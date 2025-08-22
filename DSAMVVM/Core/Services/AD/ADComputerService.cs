using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System.DirectoryServices;

namespace DSAMVVM.Core.Services.AD
{
    public class ADComputerService
    {
        private readonly string _ldapPath;

        public ADComputerService(string ldapPath)
        {
            _ldapPath = ldapPath;
        }

        public Task<ADComputerInfo> GetComputerAsync(string hostname)
        {
            return Task.Run(() =>
            {
                var info = new ADComputerInfo { Name = hostname };

                try
                {
                    using var entry = new DirectoryEntry(_ldapPath);
                    using var searcher = new DirectorySearcher(entry)
                    {
                        Filter = $"(&(objectCategory=computer)(cn={hostname}))"
                    };

                    var result = searcher.FindOne();
                    if (result != null)
                    {
                        info.Exists = true;

                        using var computer = result.GetDirectoryEntry();
                        string? dn = computer.Properties["distinguishedName"].Value?.ToString();
                        if (dn != null)
                        {
                            info.OUs = string.Join(", ", dn.Split(',').Where(p => p.StartsWith("OU=")));
                        }

                        info.Description = computer.Properties["description"]?.Value?.ToString();
                        info.OperatingSystem = computer.Properties["operatingSystem"]?.Value?.ToString() ?? "Unknown";
                        info.IsHybridGroupMember = IsInHybridGroup(computer);

                        object? enabledObj = computer.Properties["userAccountControl"]?.Value;
                        if (enabledObj is int uac)
                        {
                            // Bit 2 (0x2) = disabled
                            info.Enabled = (uac & 0x2) == 0;
                        }
                        else
                        {
                            info.Enabled = false;
                        }
                    }
                    else
                    {
                        info.Exists = false;
                        info.ErrorMessage = $"Computer name {hostname} not found.";
                        UiNotify.Warn($"Computer '{hostname}' not found in AD.");
                    }
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

        private static bool IsInHybridGroup(DirectoryEntry computer)
        {
            var memberOf = computer.Properties["memberOf"];
            if (memberOf == null) return false;

            foreach (var group in memberOf)
            {
                if (group?.ToString()?.Contains("UA-MEMHybridDevices", StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }

            return false;
        }
    }
}
