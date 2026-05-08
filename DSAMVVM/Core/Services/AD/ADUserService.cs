using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System.DirectoryServices;
using System.Text.RegularExpressions;

namespace DSAMVVM.Core.Services.AD
{
    public class ADUserService(string ldap)
    {
        private readonly string _ldap = ldap;

        public Task<ADUserInfo> GetUserAsync(string netid)
        {
            return Task.Run(() =>
            {
                var info = new ADUserInfo { Name = netid };

                try
                {
                    // Single, tight user search with required attributes preloaded
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

                    // Quick, informational lockout check (computed bit only)
                    info.Locked = DirectoryUtility.GetLockedQuick(r);

                    var dept = DirectoryUtility.GetString(r, "department")
                               ?? DirectoryUtility.GetString(r, "Department")
                               ?? "None";
                    info.DepartmentName = dept;
                    info.DepartmentNumber = dept?.Length >= 4 ? dept[..4] : (string?)null;

                    info.EduAffiliation = DirectoryUtility.GetString(r, "eduPersonPrimaryAffiliation") ?? "Unknown";

                    // Capture raw to return to UI if "unknown"
                    var rawLicense = DirectoryUtility.GetString(r, "extensionAttribute15") ?? "";
                    info.RawLicense = rawLicense;
                    info.License = ParseLicense(rawLicense);

                    var userDn = DirectoryUtility.GetString(r, "distinguishedName");

                    info.HasMimWrkstGroup = !string.IsNullOrWhiteSpace(userDn) &&
                                            DirectoryUtility.HasMimWrkstGroup(_ldap, userDn!);

                    // Second query only if needed to resolve Division rollup group
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
            if (string.IsNullOrWhiteSpace(license)) return "No valid O365 license found";

            var m = Regex.Match(license,
                @"([om]\d{3})\s*([A-Za-z]+)\s*[-_ ]*\s*(A\d+|E\d+|EXP\d+)",
                RegexOptions.IgnoreCase);

            if (m.Success)
            {
                string role = m.Groups[2].Value.ToLowerInvariant();
                string tier = m.Groups[3].Value.ToUpperInvariant();

                string roleText = role switch
                {
                    "stuw" => "Student Worker",
                    "emp" => "Employee",
                    "stu" => "Student",
                    "dc" => "Alumni",
                    "admit" => "Admit",
                    _ => "Unknown"
                };

                string tierText = tier.StartsWith("EXP", StringComparison.Ordinal)
                    ? $"Exchange P{tier[3..]}"
                    : tier;

                return $"{roleText} {tierText}".Trim();
            }

            foreach (var segment in license.Split('(', ')'))
                if (segment.Contains("365", StringComparison.OrdinalIgnoreCase))
                    return segment.Trim() + " (Unknown Type)";

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

        // Fetches a user's group membership and evaluates it for Adobe entitlements
        public Task<AdobeLicenseStatus> CheckAdobeLicensesAsync(string netid)
        {
            return Task.Run(() =>
            {
                try
                {
                    var groups = DirectoryUtility.GetUserGroupsBySam(_ldap, netid);
                    return EvaluateAdobeLicenses(groups);
                }
                catch (Exception)
                {
                    UiNotify.Warn($"Could not retrieve Adobe licensing groups for '{netid}'.");
                    return new AdobeLicenseStatus(false, false);
                }
            });
        }

        // Evaluates a provided collection of Active Directory groups to determine Adobe software entitlements
        public static AdobeLicenseStatus EvaluateAdobeLicenses(IEnumerable<string>? userGroups)
        {
            bool hasPro = false;
            bool hasCc = false;

            if (userGroups == null)
            {
                return new AdobeLicenseStatus(false, false);
            }

            foreach (var group in userGroups)
            {
                if (group.Contains("adobesync-acrobat-pro", StringComparison.OrdinalIgnoreCase))
                {
                    hasPro = true;
                }
                else if (group.Contains("adobesync-cc-campus", StringComparison.OrdinalIgnoreCase) ||
                         group.Contains("adobesync-cc-student", StringComparison.OrdinalIgnoreCase))
                {
                    hasCc = true;
                }

                if (hasPro && hasCc)
                {
                    break;
                }
            }

            return new AdobeLicenseStatus(hasPro, hasCc);
        }
    }
}