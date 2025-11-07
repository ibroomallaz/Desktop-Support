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

                    //quick, informational lockout check (computed bit only)
                    info.Locked = DirectoryUtility.GetLockedQuick(r);

                    var dept = DirectoryUtility.GetString(r, "department")
                               ?? DirectoryUtility.GetString(r, "Department")
                               ?? "None";
                    info.DepartmentName = dept;
                    info.DepartmentNumber = dept?.Length >= 4 ? dept[..4] : (string?)null;

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
            // Guard: avoid null/empty and wasted work
            if (string.IsNullOrWhiteSpace(license)) return "No valid O365 license found";

            // Find an O/M + 3 digits, then role code, then tier (A#, E#, or EXP#)
            // Not anchored, allows separators/whitespace and extra text around the token.
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
                    _ => "Unknown"
                };

                string tierText = tier.StartsWith("EXP", StringComparison.Ordinal)
                    ? $"Exchange P{tier[3..]}"   // EXP1 -> Exchange P1
                    : tier;                              // A1 / E3, etc.

                return $"{roleText} {tierText}".Trim();
            }

            // Fallback
            foreach (var segment in license.Split('(', ')'))
                if (segment.Contains("365", StringComparison.OrdinalIgnoreCase))
                    return segment.Trim() + " (Unknown License Type)";

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
