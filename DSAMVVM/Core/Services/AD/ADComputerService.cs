using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System.DirectoryServices;
using System.Reflection;

namespace DSAMVVM.Core.Services.AD
{
    public class ADComputerService(string ldapPath)
    {
        private readonly string _ldapPath = ldapPath;

        public Task<ADComputerInfo> GetComputerAsync(string hostname)
        {
            return Task.Run(() =>
            {
                var info = new ADComputerInfo { Name = hostname };

                try
                {
                    // single computer search with required attributes preloaded
                    var r = DirectoryUtility.FindComputerByCn(_ldapPath, hostname);
                    if (r == null)
                    {
                        info.Exists = false;
                        info.ErrorMessage = $"Computer name {hostname} not found.";
                        UiNotify.Warn($"Computer '{hostname}' not found in AD.");
                        return info;
                    }

                    info.Exists = true;

                    // DN -> comma-joined OU path for display
                    var dn = DirectoryUtility.GetString(r, "distinguishedName");
                    if (!string.IsNullOrEmpty(dn))
                        info.OUs = string.Join(", ", dn.Split(',').Where(p => p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase)));

                    info.Description = DirectoryUtility.GetString(r, "description");
                    info.OperatingSystem = DirectoryUtility.GetString(r, "operatingSystem") ?? "Unknown";
                    info.Enabled = DirectoryUtility.GetEnabledFromUac(r);

                    // simple membership flag from memberOf attribute when present
                    info.IsHybridGroupMember = DirectoryUtility.IsMemberOf(r, "UA-MEMHybridDevices");

                    info.LastLogonDate = ParseLastLogon(r);
                }
                catch (DirectoryServicesCOMException ex)
                {
                    info.Exists = false;
                    info.ErrorMessage = "Unable to connect to the domain controller.";
                    UiNotify.Error("AD computer lookup failed", "Domain controller is unreachable.", ex, alsoStatusBar: true);
                }
                catch (Exception ex)
                {
                    info.Exists = false;
                    info.ErrorMessage = $"Unexpected error during AD computer lookup: {ex.Message}";
                    UiNotify.Error("AD computer lookup failed", ex.Message, ex, alsoStatusBar: true);
                }

                return info;
            });
        }

        // converts AD IADsLargeInteger to a formatted local timestamp string
        private static string ParseLastLogon(SearchResult r)
        {
            if (!r.Properties.Contains("lastLogonTimestamp") || r.Properties["lastLogonTimestamp"].Count == 0)
                return "Never";

            var ts = r.Properties["lastLogonTimestamp"][0];

            try
            {
                if (ts is long longValue)
                    return DateTime.FromFileTime(longValue).ToString("MMM dd, yyyy h:mm tt");

                var type = ts.GetType();
                var highObj = type.InvokeMember("HighPart", BindingFlags.GetProperty, null, ts, null!);
                var lowObj = type.InvokeMember("LowPart", BindingFlags.GetProperty, null, ts, null!);

                if (highObj is int highPart && lowObj is int lowPart)
                {
                    var fileTime = ((long)highPart << 32) | (uint)lowPart;
                    return DateTime.FromFileTime(fileTime).ToString("MMM dd, yyyy h:mm tt");
                }

                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}