using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System.DirectoryServices;
using System.Text.RegularExpressions;

namespace DSAMVVM.Core.Services.AD
{
    public class ADUserService
    {
        private readonly string _ldap;

        public ADUserService(string ldap) { _ldap = ldap; }

        public Task<ADUserInfo> GetUserAsync(string netid)
        {
            return Task.Run(() =>
            {
                var info = new ADUserInfo { Name = netid };

                try
                {
                    // single, tight user search with required attributes preloaded
                    var r = DirectoryUtility.FindUserBySam(_ldap, netid);
                    if (r == null)
                    {
                        info.Exists = false;
                        info.ErrorMessage = "User not found.";
                        return info;
                    }

                    info.Exists = true;
                    info.DisplayName = DirectoryUtility.GetString(r, "displayName") ?? "Unknown";
                    info.Enabled = DirectoryUtility.GetEnabledFromUac(r);

                    var dept = DirectoryUtility.GetString(r, "department")
                               ?? DirectoryUtility.GetString(r, "Department")
                               ?? "None";
                    info.DepartmentName = dept;
                    info.DepartmentNumber = dept?.Length >= 4 ? dept[..4] : null;

                    info.EduAffiliation = DirectoryUtility.GetString(r, "eduPersonPrimaryAffiliation") ?? "Unknown";

                    var rawLicense = DirectoryUtility.GetString(r, "extensionAttribute15") ?? "";
                    info.License = ParseLicense(rawLicense);

                    // second query only if needed to resolve Division rollup group
                    var userDn = DirectoryUtility.GetString(r, "distinguishedName");
                    var result = !string.IsNullOrWhiteSpace(userDn)
                        ? DirectoryUtility.FindDivisionRollupGroup(_ldap, userDn!)
                        : null;

                    if (result?.Properties["cn"]?.Count > 0)
                    {
                        var cn = result.Properties["cn"][0]?.ToString();
                        info.Division = cn?.Length >= 4 ? cn[..4] : "N/A";
                    }
                    else
                    {
                        info.Division = "No Departmental MIM group";
                    }
                }
                catch (DirectoryServicesCOMException ex)
                {
                    info.Exists = false;
                    info.ErrorMessage = "Unable to connect to the directory service.";
                    UiNotify.Error("AD lookup failed", "Directory service is unreachable.", ex, alsoStatusBar: true);
                }
                catch (Exception ex)
                {
                    info.Exists = false;
                    info.ErrorMessage = $"Unexpected error: {ex.Message}";
                    UiNotify.Error("AD lookup failed", ex.Message, ex, alsoStatusBar: true);
                }

                return info;
            });
        }

        private static string ParseLicense(string license)
        {
            // matches original semantics; returns human readable classification
            string pattern = "([om]{1}\\d{3})([A-Z]+)([AE]\\d{1})";
            var match = Regex.Match(license, pattern);
            if (match.Success)
            {
                string group2 = match.Groups[2].Value;
                string group3 = match.Groups[3].Value;
                return group2.ToLower() switch
                {
                    "stuw" => $"Student Worker {group3}",
                    "emp" => $"Employee {group3}",
                    "stu" => $"Student {group3}",
                    _ => $"Unknown {group3}"
                };
            }

            foreach (var segment in license.Split('(', ')'))
                if (segment.Contains("365")) return segment + " (Unknown License Type)";

            return "No valid O365 license found";
        }

        public Task<string?> LookupNameByEmployeeID(string userNumber)
        {
            return Task.Run(() =>
            {
                try
                {
                    var r = DirectoryUtility.FindUserByEmployeeId(_ldap, userNumber);
                    if (r != null) return DirectoryUtility.GetString(r, "displayName");
                }
                catch
                {
                    UiNotify.Warn($"Could not resolve displayName for employeeID '{userNumber}'.");
                }
                return null;
            });
        }
    }
}
