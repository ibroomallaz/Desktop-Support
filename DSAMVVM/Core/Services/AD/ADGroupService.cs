using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System.DirectoryServices;


namespace DSAMVVM.Core.Services.AD
{
    public class ADGroupService
    {
        private static DirectoryEntry Root() => new(Globals.g_domainPathLDAP);

        public static Task<MimLookupResult> GetUserMimGroupsAsync(string netid) =>
            Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(netid))
                    return new MimLookupResult { Exists = false, Error = "Empty NetID." };

                try
                {
                    // user lookup loads memberOf so we can pull direct MIM memberships
                    var sr = DirectoryUtility.FindUserBySam(Globals.g_domainPathLDAP, netid);
                    if (sr == null)
                        return new MimLookupResult { Exists = false, Error = "User not found." };

                    var groups = new List<string>();

                    // direct memberships
                    var memberships = DirectoryUtility.GetStrings(sr, "memberOf");
                    if (memberships.Count > 0)
                    {
                        foreach (var dn in memberships)
                        {
                            if (string.IsNullOrEmpty(dn)) continue;
                            var cn = DnToCn(dn); // CN=Foo,OU=Bar -> Foo
                            if (cn.Contains("MIM", StringComparison.OrdinalIgnoreCase))
                                groups.Add(cn);
                        }
                    }

                    // fallback if memberOf is empty/filtered: group search by member DN
                    if (groups.Count == 0)
                    {
                        var userDn = DirectoryUtility.GetString(sr, "distinguishedName");
                        if (!string.IsNullOrWhiteSpace(userDn))
                        {
                            var byMember = DirectoryUtility.FindGroupCnsByMemberDn(Globals.g_domainPathLDAP, userDn!, "MIM");
                            if (byMember.Count > 0) groups.AddRange(byMember);
                        }
                    }

                    // preserve order while removing duplicates
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    groups = [.. groups.Where(seen.Add)];

                    return new MimLookupResult
                    {
                        Exists = true,
                        Enabled = DirectoryUtility.GetEnabledFromUac(sr),
                        Groups = groups
                    };
                }
                catch (DirectoryServicesCOMException ex)
                {
                    UiNotify.Error("AD MIM lookup failed", ex.Message, ex, alsoStatusBar: true);
                    return new MimLookupResult { Exists = false, Error = "Directory error." };
                }
                catch (Exception ex)
                {
                    UiNotify.Error("AD MIM lookup failed", ex.Message, ex, alsoStatusBar: true);
                    return new MimLookupResult { Exists = false, Error = ex.Message };
                }
            });

        public static Task<ADGroupInfo> GetGroupAsync(string groupName) =>
            Task.Run(() =>
            {
                var info = new ADGroupInfo();
                var name = Normalize(groupName);
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
                        CacheResults = true,
                        Asynchronous = true,
                        ServerTimeLimit = TimeSpan.FromSeconds(3),
                        PageSize = 0,
                        SizeLimit = 1
                    };
                    ds.ReferralChasing = ReferralChasingOption.None;
                    ds.PropertiesToLoad.Add("member");

                    var sr = ds.FindOne();
                    if (sr == null)
                    {
                        info.Exists = false; info.ErrorMessage = "Group not found."; info.GroupMembers = []; info.MemberCount = 0;
                        return info;
                    }

                    // ranged attribute read handles very large groups safely
                    var membersDn = DirectoryUtility.GetAllMemberDns(sr);
                    var members = new List<string>(capacity: membersDn.Count);
                    foreach (var dn in membersDn)
                        if (!string.IsNullOrEmpty(dn)) members.Add(DnToCn(dn)); // CN=Foo,OU=Bar -> Foo

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

        private static string Normalize(string s)
        {
            s = s.Trim();
            var allDigits = s.All(char.IsDigit);
            if (allDigits && s.Length == 5) return $"UA-MIM-{s}";
            if (allDigits && s.Length == 4) return $"UA-MIM-0{s}";
            return s;
        }

        private static string Esc(string s) =>
            s.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "");

        private static string DnToCn(string dn)
        {
            // DN -> CN
            var i = dn.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return dn;
            var rest = dn[(i + 3)..];
            var j = rest.IndexOf(',');
            return j > 0 ? rest[..j] : rest;
        }
    }
}
