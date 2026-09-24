using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Services
{
    public sealed class NetworkDetectionService : INetworkDetectionService
    {
        private const string CampusDomainSuffix = "arizona.edu";
        private const string CampusSsid = "UAWiFi";

        private NetworkStateInfo _currentState = new();
        private readonly Timer _debounceTimer;
        private readonly object _lock = new();

        public NetworkStateInfo CurrentState
        {
            get { lock (_lock) return _currentState; }
            private set
            {
                lock (_lock) _currentState = value;
                NetworkStateChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<NetworkStateInfo>? NetworkStateChanged;

        public NetworkDetectionService()
        {
            // Debounce reactive network events by 600ms to allow DHCP / adapter negotiation to settle
            _debounceTimer = new Timer(_ => Refresh(), null, Timeout.Infinite, Timeout.Infinite);

            try
            {
                NetworkChange.NetworkAddressChanged += OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            }
            catch (Exception ex)
            {
                Log.Warn("NetworkService", $"Failed to subscribe to network change events: {ex.Message}");
            }

            // Run initial detection
            Refresh();
        }

        private void OnNetworkChanged(object? sender, EventArgs e) =>
            _debounceTimer.Change(600, Timeout.Infinite);

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) =>
            _debounceTimer.Change(600, Timeout.Infinite);

        public NetworkStateInfo Refresh()
        {
            var detected = DetectCurrentState();
            CurrentState = detected;
            return detected;
        }

        public Task<NetworkStateInfo> RefreshAsync() => Task.Run(Refresh);

        private static NetworkStateInfo DetectCurrentState()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .ToList();

                if (interfaces.Count == 0 || !NetworkInterface.GetIsNetworkAvailable())
                {
                    return NetworkStateInfo.Disconnected();
                }

                // 1. Cisco Secure Client / Cisco AnyConnect Check (Priority 1)
                var ciscoVpn = interfaces.FirstOrDefault(nic =>
                    (nic.Description.Contains("Cisco Secure Client", StringComparison.OrdinalIgnoreCase) ||
                     nic.Description.Contains("Cisco AnyConnect", StringComparison.OrdinalIgnoreCase) ||
                     nic.Name.Contains("Cisco", StringComparison.OrdinalIgnoreCase)) &&
                    HasUsableIpv4(nic));

                if (ciscoVpn != null)
                {
                    return new NetworkStateInfo
                    {
                        ConnectionType = NetworkConnectionType.Vpn,
                        Label = "Cisco Secure Client VPN",
                        AdapterName = ciscoVpn.Description,
                        PrimaryIp = GetIpv4Address(ciscoVpn),
                        DnsSuffix = ciscoVpn.GetIPProperties().DnsSuffix
                    };
                }

                // 2. Campus Wired Check (Priority 2)
                var wiredCampus = interfaces.FirstOrDefault(nic =>
                    nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                    !IsVirtualAdapter(nic) &&
                    HasUsableIpv4(nic) &&
                    IsCampusDns(nic));

                if (wiredCampus != null)
                {
                    return new NetworkStateInfo
                    {
                        ConnectionType = NetworkConnectionType.CampusWired,
                        Label = "Campus Network (Wired)",
                        AdapterName = wiredCampus.Description,
                        PrimaryIp = GetIpv4Address(wiredCampus),
                        DnsSuffix = wiredCampus.GetIPProperties().DnsSuffix
                    };
                }

                // 3. Campus Wi-Fi Check (Priority 3 - UAWiFi ONLY)
                var wifiAdapter = interfaces.FirstOrDefault(nic =>
                    nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                    HasUsableIpv4(nic));

                if (wifiAdapter != null)
                {
                    string? currentSsid = GetConnectedWifiSsid();

                    if (!string.IsNullOrWhiteSpace(currentSsid))
                    {
                        var ip = GetIpv4Address(wifiAdapter);

                        if (string.Equals(currentSsid, CampusSsid, StringComparison.OrdinalIgnoreCase))
                        {
                            return new NetworkStateInfo
                            {
                                ConnectionType = NetworkConnectionType.CampusWiFi,
                                Label = "Campus Wi-Fi (UAWiFi)",
                                AdapterName = wifiAdapter.Description,
                                Ssid = currentSsid,
                                PrimaryIp = ip,
                                DnsSuffix = wifiAdapter.GetIPProperties().DnsSuffix
                            };
                        }

                        // Connected to Wi-Fi, but NOT UAWiFi (e.g. UAGuest, eduroam, home Wi-Fi)
                        return new NetworkStateInfo
                        {
                            ConnectionType = NetworkConnectionType.OffCampus,
                            Label = $"Off-Campus ({currentSsid})",
                            AdapterName = wifiAdapter.Description,
                            Ssid = currentSsid,
                            PrimaryIp = ip,
                            Details = string.Equals(currentSsid, "eduroam", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(currentSsid, "UAGuest", StringComparison.OrdinalIgnoreCase)
                                ? $"{currentSsid} is restricted. Connect to Cisco Secure Client for internal access."
                                : null
                        };
                    }
                }

                // 4. Any other active physical connection (e.g. Non-campus Ethernet at home)
                var activePhysical = interfaces.FirstOrDefault(nic => !IsVirtualAdapter(nic) && HasUsableIpv4(nic));
                if (activePhysical != null)
                {
                    return new NetworkStateInfo
                    {
                        ConnectionType = NetworkConnectionType.OffCampus,
                        Label = "Off-Campus / External",
                        AdapterName = activePhysical.Description,
                        PrimaryIp = GetIpv4Address(activePhysical),
                        DnsSuffix = activePhysical.GetIPProperties().DnsSuffix
                    };
                }

                // 5. Check if any physical adapter is connected but stuck on APIPA (169.254.x.x - DHCP failed)
                var apipaAdapter = interfaces.FirstOrDefault(nic =>
                    !IsVirtualAdapter(nic) &&
                    (GetIpv4Address(nic)?.StartsWith("169.254.") == true));

                if (apipaAdapter != null)
                {
                    return new NetworkStateInfo
                    {
                        ConnectionType = NetworkConnectionType.Disconnected,
                        Label = "No Internet (APIPA)",
                        AdapterName = apipaAdapter.Description,
                        PrimaryIp = GetIpv4Address(apipaAdapter),
                        Details = "Self-assigned IP address. DHCP server did not respond."
                    };
                }

                // 6. Only virtual adapters remain or no network interface has usable connectivity
                return NetworkStateInfo.Disconnected();
            }
            catch (Exception ex)
            {
                Log.Error("NetworkDetection", $"Error detecting network state: {ex.Message}");
                return new NetworkStateInfo
                {
                    ConnectionType = NetworkConnectionType.Disconnected,
                    Label = "Detection Error",
                    Details = ex.Message
                };
            }
        }

        private static bool IsCampusDns(NetworkInterface nic)
        {
            var props = nic.GetIPProperties();
            var suffix = props.DnsSuffix;
            if (!string.IsNullOrWhiteSpace(suffix) &&
                (suffix.Equals(CampusDomainSuffix, StringComparison.OrdinalIgnoreCase) ||
                 suffix.EndsWith("." + CampusDomainSuffix, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return props.DnsAddresses.Any(ip =>
                ip.ToString().StartsWith("128.196.") ||
                ip.ToString().StartsWith("150.135."));
        }

        private static bool IsVirtualAdapter(NetworkInterface nic)
        {
            var desc = nic.Description;
            var name = nic.Name;
            return desc.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
                   desc.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                   desc.Contains("WSL", StringComparison.OrdinalIgnoreCase) ||
                   desc.Contains("Npcap", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase) ||
                   desc.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetIpv4Address(NetworkInterface nic)
        {
            return nic.GetIPProperties().UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?
                .Address.ToString();
        }

        private static bool HasUsableIpv4(NetworkInterface nic)
        {
            var ip = GetIpv4Address(nic);
            return !string.IsNullOrEmpty(ip) &&
                   !ip.StartsWith("169.254.", StringComparison.Ordinal) &&
                   !ip.StartsWith("127.", StringComparison.Ordinal);
        }

        private static string? GetConnectedWifiSsid()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(1500);

                var match = Regex.Match(output, @"^\s*SSID\s*:\s*(.+)$", RegexOptions.Multiline);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Fallback / ignore process start issues
            }
            return null;
        }

        public void Dispose()
        {
            _debounceTimer.Dispose();
            try
            {
                NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            }
            catch
            {
                // ignored
            }
        }
    }
}
