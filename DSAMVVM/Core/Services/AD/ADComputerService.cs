using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System.DirectoryServices;

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
    }
}
