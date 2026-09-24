using System.Text;
using DSAMVVM.Core.Enums;

namespace DSAMVVM.Core.Models
{
    public sealed record NetworkStateInfo
    {
        public NetworkConnectionType ConnectionType { get; init; } = NetworkConnectionType.Disconnected;
        public string Label { get; init; } = "Disconnected";
        public string? AdapterName { get; init; }
        public string? Ssid { get; init; }
        public string? PrimaryIp { get; init; }
        public string? DnsSuffix { get; init; }
        public string? Details { get; init; }

        public bool IsConnected => ConnectionType != NetworkConnectionType.Disconnected;

        public static NetworkStateInfo Disconnected(string? details = "No active network connection detected.") =>
            new()
            {
                ConnectionType = NetworkConnectionType.Disconnected,
                Label = "Disconnected",
                Details = details
            };

        public string ToolTipText
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine(Label);
                if (!string.IsNullOrWhiteSpace(AdapterName))
                    sb.AppendLine($"Adapter: {AdapterName}");
                if (!string.IsNullOrWhiteSpace(Ssid))
                    sb.AppendLine($"SSID: {Ssid}");
                if (!string.IsNullOrWhiteSpace(PrimaryIp))
                    sb.AppendLine($"IP: {PrimaryIp}");
                if (!string.IsNullOrWhiteSpace(DnsSuffix))
                    sb.AppendLine($"DNS Suffix: {DnsSuffix}");
                if (!string.IsNullOrWhiteSpace(Details))
                    sb.AppendLine(Details);
                sb.Append("Click to refresh status");
                return sb.ToString();
            }
        }
    }
}
