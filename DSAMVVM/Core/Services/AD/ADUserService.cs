using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services.AD
{
    public class ADUserService
    {
        private readonly string _domain;
        private readonly string _ldap;

        public ADUserService(string domain, string ldap)
        {
            _domain = domain;
            _ldap = ldap;
        }

        public Task<ADUserInfo> GetUserAsync(string netid)
        {
            return Task.Run(() =>
            {
                var info = new ADUserInfo { Name = netid };

                try
                {
                    using var context = new PrincipalContext(ContextType.Domain, _domain);
                    var user = UserPrincipal.FindByIdentity(context, netid);

                    if (user == null)
                    {
                        info.Exists = false;
                        info.ErrorMessage = "User not found.";
                        return info;
                    }

                    info.Exists = true;
                    info.DisplayName = user.DisplayName ?? "Unknown";
                    info.Enabled = user.Enabled ?? false;

                    var dirEntry = (DirectoryEntry)user.GetUnderlyingObject();
                    info.DepartmentName = dirEntry.Properties["Department"]?.Value?.ToString() ?? "None";
                    info.DepartmentNumber = info.DepartmentName?.Length >= 4 ? info.DepartmentName[..4] : null;
                    info.EduAffiliation = dirEntry.Properties["eduPersonPrimaryAffiliation"]?.Value?.ToString() ?? "Unknown";

                    var rawLicense = dirEntry.Properties["extensionattribute15"]?.Value?.ToString() ?? "";
                    info.License = ParseLicense(rawLicense);

                    using var searcher = new DirectorySearcher(context.ConnectedServer)
                    {
                        Filter = $"(&(objectCategory=group)(member={user.DistinguishedName})(cn=*MIM-DivisionRollup*))"
                    };

                    var result = searcher.FindOne();
                    if (result?.Properties["cn"]?.Count > 0)
                    {
                        var group = result.Properties["cn"][0]?.ToString();
                        info.Division = group?.Length >= 4 ? group[..4] : "N/A";
                    }
                    else
                    {
                        info.Division = "No Departmental MIM group";
                    }
                }
                catch (PrincipalServerDownException ex)
                {
                    info.ErrorMessage = "Unable to connect to the domain controller.";
                    info.Exists = false;

                    // Surface -  actionable for the user (VPN/connection issues)
                    UiNotify.Error("AD lookup failed", "Domain controller is unreachable.", ex, alsoStatusBar: true);
                }
                catch (Exception ex)
                {
                    info.ErrorMessage = $"Unexpected error: {ex.Message}";
                    info.Exists = false;

                    // Bubble a concise error and log details; also echo to status bar.
                    UiNotify.Error("AD lookup failed", ex.Message, ex, alsoStatusBar: true);
                }

                return info;
            });
        }

        public Task<List<string>> GetMimGroupsAsync(string netid)
        {
            return Task.Run(() =>
            {
                var mimGroups = new List<string>();

                try
                {
                    using var context = new PrincipalContext(ContextType.Domain, _domain);
                    var user = UserPrincipal.FindByIdentity(context, netid);

                    if (user != null)
                    {
                        mimGroups = user.GetGroups()?
                            .Where(g => g.Name.Contains("MIM"))
                            .Select(g => g.Name)
                            .ToList() ?? [];
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal; warn and continue with empty list
                    UiNotify.Warn($"Could not enumerate MIM groups for '{netid}'.");
                }

                return mimGroups;
            });
        }

        private static string ParseLicense(string license)
        {
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
            {
                if (segment.Contains("365"))
                    return segment + " (Unknown License Type)";
            }

            return "No valid O365 license found";
        }

        // Keeping logic for now but deprecating search until later
        public Task<string?> LookupNameByEmployeeID(string userNumber)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var entry = new DirectoryEntry(_ldap);
                    using var searcher = new DirectorySearcher(entry)
                    {
                        Filter = $"(&(objectClass=user)(employeeID={userNumber}))"
                    };

                    searcher.PropertiesToLoad.Add("displayName");
                    searcher.PropertiesToLoad.Add("employeeID");

                    var result = searcher.FindOne();
                    if (result != null)
                        return result.Properties["displayName"][0]?.ToString();
                }
                catch (Exception ex)
                {
                    // Non-fatal lookup; warn and return null
                    UiNotify.Warn($"Could not resolve displayName for employeeID '{userNumber}'.");
                }

                return null;
            });
        }
    }
}
