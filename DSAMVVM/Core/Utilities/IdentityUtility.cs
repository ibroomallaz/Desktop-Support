using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.DirectoryServices.AccountManagement;

namespace DSAMVVM.Core.Utilities;
public static class IdentityUtility
{
    static string? _cached;

    // P/Invoke to tap into native Windows Security API (Catches Entra ID Display Names)
    [DllImport("secur32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetUserNameEx(int nameFormat, StringBuilder userName, ref uint userNameSize);
    private const int NameDisplay = 3; // 3 requests the "Display Name" format

    public static string GetFirstName()
    {
        if (!string.IsNullOrEmpty(_cached)) return _cached!;

        // 1. Try Claims first (fastest, works if WAM caching is active)
        var id = WindowsIdentity.GetCurrent();
        var claim = id?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
        if (!string.IsNullOrWhiteSpace(claim)) return _cached = claim!;

        // 2. Try Traditional AD (Fails immediately on Pure Entra machines)
        try
        {
            var up = UserPrincipal.Current;
            if (!string.IsNullOrWhiteSpace(up?.GivenName))
                return _cached = up.GivenName;

            if (!string.IsNullOrWhiteSpace(up?.DisplayName))
                return _cached = ParseFirstName(up.DisplayName);
        }
        catch { /* Swallowed: Expected on Pure Entra or Off-VPN */ }

        // 3. Try Native Windows API (Entra ID Fix)
        try
        {
            uint size = 1024;
            var sb = new StringBuilder((int)size);

            // This asks the OS: "What is the human-readable name of the current user?"
            if (GetUserNameEx(NameDisplay, sb, ref size))
            {
                var nativeDisplayName = sb.ToString();
                if (!string.IsNullOrWhiteSpace(nativeDisplayName))
                    return _cached = ParseFirstName(nativeDisplayName);
            }
        }
        catch { /* Swallowed: Failsafe */ }

        // 4. Ultimate Fallback: NetID
        var netId = Environment.UserName;
        return _cached = string.IsNullOrEmpty(netId) ? "User" : char.ToUpper(netId[0]) + netId.Substring(1);
    }

    // Helper method to keep the parsing logic clean
    private static string ParseFirstName(string displayName)
    {
        var parts = displayName.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        //"LastName, FirstName"
        if (displayName.Contains(",") && parts.Length > 1)
            return parts[1].Trim();

        //"FirstName LastName"
        return parts[0].Trim();
    }
}