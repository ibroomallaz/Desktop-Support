using DSAMVVM.Core;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System.DirectoryServices;

namespace DSAMVVM.Core.Services.AD
{
    public class ADGroupService
    {
        // Bind directly to configured LDAP base from Globals
        private static DirectoryEntry Root() => new DirectoryEntry(Globals.g_domainPathLDAP);

        // User → direct MIM groups (single read: memberOf + userAccountControl)
        public Task<MimLookupResult> GetUserMimGroupsAsync(string netid) =>
            Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(netid))
                    return new MimLookupResult { Exists = false, Error = "Empty NetID." };

                try
                {
                    using var root = Root();
                    using var ds = new DirectorySearcher(root)
                    {
                        // accept sAMAccountName or UPN (prefix only); no deep expansion
                        Filter = $"(&(objectCategory=person)(objectClass=user)(|(sAMAccountName={Esc(netid)})(userPrincipalName={Esc(netid)}@*)))",
                        SearchScope = SearchScope.Subtree,
                        PageSize = 1,
                        SizeLimit = 1
                    };
                    ds.PropertiesToLoad.Add("userAccountControl");
                    ds.PropertiesToLoad.Add("memberOf");

                    var sr = ds.FindOne();
                    if (sr == null) return new MimLookupResult { Exists = false, Error = "User not found." };

                    // Enabled = !DISABLED (0x2) bit
                    int? uac = sr.Properties["userAccountControl"]?.Count > 0 ? (int?)sr.Properties["userAccountControl"][0] : null;
                    bool? enabled = uac.HasValue ? (uac.Value & 0x2) == 0 : (bool?)null;

                    // Filter direct memberships that contain "MIM"; show CN only
                    var groups = new List<string>();
                    var mo = sr.Properties["memberOf"];
                    if (mo is { Count: > 0 })
                        for (int i = 0; i < mo.Count; i++)
                        {
                            var dn = mo[i]?.ToString();
                            if (dn != null && dn.IndexOf("MIM", StringComparison.OrdinalIgnoreCase) >= 0)
                                groups.Add(DnToCn(dn));
                        }

                    return new MimLookupResult { Exists = true, Enabled = enabled, Groups = groups };
                }
                catch (DirectoryServicesCOMException ex) // directory/LDAP failure
                {
                    UiNotify.Error("AD MIM lookup failed", ex.Message, ex, alsoStatusBar: true);
                    return new MimLookupResult { Exists = false, Error = "Directory error." };
                }
                catch (Exception ex) // anything else
                {
                    UiNotify.Error("AD MIM lookup failed", ex.Message, ex, alsoStatusBar: true);
                    return new MimLookupResult { Exists = false, Error = ex.Message };
                }
            });

        // Group → direct members (single read: member attribute)
        public Task<ADGroupInfo> GetGroupAsync(string groupName) =>
            Task.Run(() =>
            {
                var info = new ADGroupInfo();
                var name = Normalize(groupName); // allows "1234" → "UA-MIM-01234"
                if (string.IsNullOrWhiteSpace(name))
                {
                    info.Exists = false; info.ErrorMessage = "Empty group name."; info.GroupMembers = []; info.MemberCount = 0;
                    return info;
                }

                try
                {
                    using var root = Root();
                    using var ds = new DirectorySearcher(root)
                    {
                        // match by CN or sAMAccountName
                        Filter = $"(&(objectClass=group)(|(cn={Esc(name)})(sAMAccountName={Esc(name)})))",
                        SearchScope = SearchScope.Subtree,
                        PageSize = 1,
                        SizeLimit = 1
                    };
                    ds.PropertiesToLoad.Add("member");

                    var sr = ds.FindOne();
                    if (sr == null)
                    {
                        info.Exists = false; info.ErrorMessage = "Group not found."; info.GroupMembers = []; info.MemberCount = 0;
                        return info;
                    }

                    // Convert each member DN to CN (no nested expansion)
                    var members = new List<string>();
                    var m = sr.Properties["member"];
                    if (m is { Count: > 0 })
                        for (int i = 0; i < m.Count; i++)
                        {
                            var dn = m[i]?.ToString();
                            if (dn != null) members.Add(DnToCn(dn));
                        }

                    info.Exists = true;
                    info.GroupMembers = members;
                    info.MemberCount = members.Count;
                    return info;
                }
                catch (DirectoryServicesCOMException ex)
                {
                    info.Exists = false; info.ErrorMessage = "Unable to query directory."; info.GroupMembers = [info.ErrorMessage]; info.MemberCount = 0;
                    UiNotify.Error("AD group lookup failed", ex.Message, ex, alsoStatusBar: true);
                    return info;
                }
                catch (Exception ex)
                {
                    info.Exists = false; info.ErrorMessage = $"Error retrieving group: {ex.Message}"; info.GroupMembers = [info.ErrorMessage]; info.MemberCount = 0;
                    UiNotify.Error("AD group lookup failed", ex.Message, ex, alsoStatusBar: true);
                    return info;
                }
            });

        // "1234" → "UA-MIM-01234"
        private static string Normalize(string s)
        {
            s = s.Trim();
            return (s.Length == 4 && int.TryParse(s, out _)) ? $"UA-MIM-0{s}" : s;
        }

        // basic LDAP filter escaping
        private static string Esc(string s) =>
            s.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "");

        // "CN=Some Name,OU=..." → "Some Name"
        private static string DnToCn(string dn)
        {
            var i = dn.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return dn;
            var rest = dn[(i + 3)..];
            var j = rest.IndexOf(',');
            return j > 0 ? rest[..j] : rest;
        }
    }
}
