namespace DSAMVVM.Core.Utilities;

using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;

public static class IdentityUtility
{
    static string? _cached;

    public static string GetFirstName()
    {
        if (!string.IsNullOrEmpty(_cached)) return _cached!;

        var id = WindowsIdentity.GetCurrent();
        var claim = id?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
        if (!string.IsNullOrWhiteSpace(claim)) return _cached = claim!;

        try
        {
            var up = UserPrincipal.Current;
            if (!string.IsNullOrWhiteSpace(up?.GivenName)) return _cached = up!.GivenName!;
            if (!string.IsNullOrWhiteSpace(up?.DisplayName))
                return _cached = up!.DisplayName!.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)[0];
        }
        catch { }

        return _cached = System.Environment.UserName;
    }
}
