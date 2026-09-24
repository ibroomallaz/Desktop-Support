using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using System.Diagnostics;
using System.DirectoryServices;
using System.Net;
using System.Net.Sockets;
using Timer = System.Timers.Timer;

namespace DSAMVVM.Core.Services.AD;

public class ADDetectionService : IADDetectionService, IDisposable
{
    private const string DefaultDcHost = "bluecat.arizona.edu";
    private const int LdapPort = 389;
    private const int ConnectTimeoutMs = 1500;

    private readonly INetworkDetectionService? _networkService;
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private readonly Timer _debounceTimer;

    private ADStateInfo _currentState = ADStateInfo.Checking();
    private bool _disposed;

    public event Action<ADStateInfo>? StateChanged;

    public ADStateInfo CurrentState
    {
        get => _currentState;
        private set
        {
            _currentState = value;
            StateChanged?.Invoke(_currentState);
        }
    }

    public ADDetectionService(INetworkDetectionService? networkService = null)
    {
        _networkService = networkService;

        _debounceTimer = new Timer(700) { AutoReset = false };
        _debounceTimer.Elapsed += async (_, _) =>
        {
            try
            {
                await ProbeDomainControllerAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Debug("ADDetection", $"Debounced probe error: {ex.Message}");
            }
        };

        if (_networkService != null)
        {
            _networkService.NetworkStateChanged += OnNetworkStateChanged;

            // Trigger initial probe if already connected
            if (_networkService.CurrentState.IsConnected)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(300).ConfigureAwait(false);
                    await ProbeDomainControllerAsync().ConfigureAwait(false);
                });
            }
            else
            {
                CurrentState = ADStateInfo.Unreachable("Network disconnected");
            }
        }
        else
        {
            // Fallback initial probe
            _ = Task.Run(() => ProbeDomainControllerAsync());
        }
    }

    private void OnNetworkStateChanged(object? sender, NetworkStateInfo state)
    {
        if (!state.IsConnected)
        {
            _debounceTimer.Stop();
            CurrentState = ADStateInfo.Unreachable("Network disconnected");
            return;
        }

        // Debounce probe when network switches (e.g. VPN connecting)
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public async Task<ADStateInfo> ProbeDomainControllerAsync(CancellationToken ct = default)
    {
        if (!await _probeLock.WaitAsync(0, ct).ConfigureAwait(false))
        {
            // Probe already in progress, return current state
            return CurrentState;
        }

        try
        {
            // Fast network pre-check
            if (_networkService is { CurrentState.IsConnected: false })
            {
                CurrentState = ADStateInfo.Unreachable("Network disconnected");
                return CurrentState;
            }

            // Step 1: DNS Resolution of campus domain controller pool
            IPAddress[] addresses;
            try
            {
                using var dnsCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(ConnectTimeoutMs));
                using var linkedDnsCts = CancellationTokenSource.CreateLinkedTokenSource(dnsCts.Token, ct);
                addresses = await Dns.GetHostAddressesAsync(DefaultDcHost, linkedDnsCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                CurrentState = ADStateInfo.Unreachable($"DNS lookup failed for {DefaultDcHost} ({ex.Message})");
                return CurrentState;
            }

            if (addresses.Length == 0)
            {
                CurrentState = ADStateInfo.Unreachable($"No DNS IP records found for {DefaultDcHost}");
                return CurrentState;
            }

            var targetIp = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];

            // Step 2: TCP Handshake to Port 389 (LDAP) with precise latency measurement
            using var tcpClient = new TcpClient(targetIp.AddressFamily);
            var sw = Stopwatch.StartNew();

            try
            {
                using var tcpCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(ConnectTimeoutMs));
                using var linkedTcpCts = CancellationTokenSource.CreateLinkedTokenSource(tcpCts.Token, ct);

                await tcpClient.ConnectAsync(targetIp, LdapPort, linkedTcpCts.Token).ConfigureAwait(false);
                sw.Stop();
            }
            catch (Exception ex)
            {
                CurrentState = ADStateInfo.Unreachable($"LDAP port 389 unreachable ({ex.Message})");
                return CurrentState;
            }

            var latencyMs = (int)sw.ElapsedMilliseconds;

            // Step 3: Lightweight Directory Bind check
            var ldapResponding = CheckLdapRootDse(DefaultDcHost);

            CurrentState = ADStateInfo.Reachable(
                DefaultDcHost,
                targetIp.ToString(),
                latencyMs,
                ldapResponding
            );

            return CurrentState;
        }
        catch (Exception ex)
        {
            Log.Error("ADDetection", $"Unexpected probe failure: {ex.Message}", ex);
            CurrentState = ADStateInfo.Unreachable(ex.Message);
            return CurrentState;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    private static bool CheckLdapRootDse(string host)
    {
        try
        {
            using var rootDse = new DirectoryEntry($"LDAP://{host}/RootDSE");
            rootDse.AuthenticationType = AuthenticationTypes.FastBind | AuthenticationTypes.ReadonlyServer;
            return rootDse.Properties["defaultNamingContext"].Value != null;
        }
        catch
        {
            // Port 389 responded, even if RootDSE requires specific authentication context
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _debounceTimer.Dispose();
        _probeLock.Dispose();

        if (_networkService != null)
        {
            _networkService.NetworkStateChanged -= OnNetworkStateChanged;
        }

        GC.SuppressFinalize(this);
    }
}
