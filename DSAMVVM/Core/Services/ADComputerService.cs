using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model.AD;
using System;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    public class ADComputerService(string ldapPath, IStatusReporter status)
    {
        private readonly string _ldapPath = ldapPath;
        private readonly IStatusReporter _status = status;

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
                    }
                }
                catch (DirectoryServicesCOMException)
                {
                    info.Exists = false;
                    info.ErrorMessage = "Unable to connect to the domain controller.";
                }
                catch (Exception ex)
                {
                    info.Exists = false;
                    info.ErrorMessage = $"Unexpected error during AD computer lookup: {ex.Message}";
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
