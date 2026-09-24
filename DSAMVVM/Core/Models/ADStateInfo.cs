namespace DSAMVVM.Core.Models;

public sealed record ADStateInfo
{
    public bool IsReachable { get; init; }
    public string DcHost { get; init; } = string.Empty;
    public string? ResolvedIp { get; init; }
    public int LatencyMs { get; init; }
    public bool IsLdapResponding { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public string ToolTipText { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }

    public static ADStateInfo Reachable(string host, string? ip, int latencyMs, bool ldapResponding = true)
    {
        var latencyDisplay = latencyMs > 0 ? $"{latencyMs}ms" : "<1ms";
        var tooltip = $"Domain Controller: {host}\n" +
                      (string.IsNullOrWhiteSpace(ip) ? "" : $"IP Address: {ip}\n") +
                      $"Latency: {latencyDisplay}\n" +
                      $"LDAP Port (389): Open\n" +
                      (ldapResponding ? "Directory Bind: Verified" : "Directory Bind: Standby");

        return new ADStateInfo
        {
            IsReachable = true,
            DcHost = host,
            ResolvedIp = ip,
            LatencyMs = latencyMs,
            IsLdapResponding = ldapResponding,
            StatusText = $"{host} · {latencyDisplay}",
            ToolTipText = tooltip.Trim()
        };
    }

    public static ADStateInfo Unreachable(string reason, string host = "bluecat.arizona.edu")
    {
        return new ADStateInfo
        {
            IsReachable = false,
            DcHost = host,
            StatusText = "Unreachable (Connect to VPN)",
            ToolTipText = $"Active Directory is unreachable.\nTarget: {host}\nReason: {reason}\nConnect to Cisco Secure Client VPN or Campus Network to access AD.",
            ErrorMessage = reason
        };
    }

    public static ADStateInfo Checking(string host = "bluecat.arizona.edu")
    {
        return new ADStateInfo
        {
            IsReachable = false,
            DcHost = host,
            StatusText = "Checking...",
            ToolTipText = $"Testing reachability to {host} (Port 389)..."
        };
    }
}
