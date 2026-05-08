using System.DirectoryServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DSAMVVM.Core.Services.AD
{
    public static partial class DirectoryUtility
    {
        // user props used by UI and services
        private static readonly string[] UserProps = [
            "displayName","distinguishedName","userAccountControl",
            "department","Department","eduPersonPrimaryAffiliation","extensionAttribute15",
            "msDS-User-Account-Control-Computed"
        ];

        // computer props used by UI and services
        private static readonly string[] ComputerProps = [
            "distinguishedName","description","operatingSystem","userAccountControl","memberOf","lastLogonTimestamp"
        ];

        // ---- user lookups ----

        public static SearchResult? FindUserBySam(string ldap, string sam)
        {
            using var root = Bind(ldap);
            using var ds = NewSearcher(root,
                $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={Escape(sam)}))",
                sizeLimit: 1, props: UserProps);
            return ds.FindOne();
        }

        public static SearchResult? FindUserByEmployeeId(string ldap, string employeeId)
        {
            using var root = Bind(ldap);
            using var ds = NewSearcher(root,
                $"(&(objectCategory=person)(objectClass=user)(employeeID={Escape(employeeId)}))",
                sizeLimit: 1, props: ["displayName", "distinguishedName", "employeeID"]);
            return ds.FindOne();
        }

        public static SearchResult? FindDivisionRollupGroup(string ldap, string userDn)
        {
            using var root = Bind(ldap);
            using var ds = NewSearcher(root,
                $"(&(objectCategory=group)(member={Escape(userDn)})(cn=*MIM-DivisionRollup*))",
                sizeLimit: 1, props: ["cn"]);
            return ds.FindOne();
        }
        public static bool HasMimWrkstGroup(string ldap, string userDn)
        {
            try
            {
                using var root = Bind(ldap);
                using var ds = NewSearcher(root,
                    $"(&(objectCategory=group)(member={Escape(userDn)})(cn=UA-MIM-Wrkst-AllDivUsers))",
                    sizeLimit: 1, props: ["cn"]);

                return ds.FindOne() != null;
            }
            catch
            {
                return false;
            }
        }
        public static IReadOnlyList<string> GetUserGroupsBySam(string ldap, string sam)
        {
            using var root = Bind(ldap);
            using var ds = NewSearcher(root,
                $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={Escape(sam)}))",
                sizeLimit: 1, props: ["memberOf"]);

            var r = ds.FindOne();
            if (r == null) return [];

            return GetStrings(r, "memberOf");
        }

        // ---- computer lookups ----

        public static SearchResult? FindComputerByCn(string ldap, string cn)
        {
            using var root = Bind(ldap);
            using var ds = NewSearcher(root,
                $"(&(objectCategory=computer)(cn={Escape(cn)}))",
                sizeLimit: 1, props: ComputerProps);
            return ds.FindOne();
        }

        // ---- group helpers ----

        public static IReadOnlyList<string> FindGroupCnsByMemberDn(string ldap, string userDn, string? cnContains = null)
        {
            using var root = Bind(ldap);
            using var ds = NewSearcher(root,
                $"(&(objectCategory=group)(member={Escape(userDn)}))",
                sizeLimit: 0, pageSize: 1000, props: ["cn"]);

            var list = new List<string>();
            using var results = ds.FindAll();
            foreach (SearchResult gr in results)
            {
                var cn = GetString(gr, "cn");
                if (cn == null) continue;
                if (string.IsNullOrEmpty(cnContains) || cn.Contains(cnContains, StringComparison.OrdinalIgnoreCase))
                    list.Add(cn);
            }
            return list;
        }

        // safe, bounded ranged retrieval of very large group membership
        public static IReadOnlyList<string> GetAllMemberDns(SearchResult groupResult, int chunk = 1500, int maxMembers = 200_000, int maxIterations = 5_000)
        {
            if (chunk < 1) chunk = 1;
            if (chunk > 5000) chunk = 5000;

            using var de = groupResult.GetDirectoryEntry();
            var members = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var start = 0;
            var iters = 0;
            string? lastKey = null;

            while (iters++ < maxIterations && members.Count < maxMembers)
            {
                var ranged = $"member;range={start}-{start + chunk - 1}";
                de.RefreshCache([ranged]);

                var key = de.Properties.PropertyNames
                    .Cast<string>()
                    .FirstOrDefault(n => n.StartsWith("member;range=", StringComparison.OrdinalIgnoreCase));

                if (key == null) break;
                if (key.Equals(lastKey, StringComparison.OrdinalIgnoreCase)) break;
                lastKey = key;

                var vals = de.Properties[key];
                if (vals != null)
                {
                    foreach (var v in vals)
                    {
                        var s = v?.ToString();
                        if (!string.IsNullOrEmpty(s) && seen.Add(s)) members.Add(s);
                        if (members.Count >= maxMembers) break;
                    }
                }

                var m = DirectoryUtilRegex().Match(key);
                if (!m.Success) break;

                var endToken = m.Groups[2].Value;
                if (endToken == "*") break;
                if (!int.TryParse(endToken, out var endIndex)) break;

                var next = endIndex + 1;
                if (next <= start) break;
                start = next;
            }

            if (members.Count == 0)
            {
                var fallback = de.Properties["member"];
                if (fallback != null)
                {
                    foreach (var v in fallback)
                    {
                        var s = v?.ToString();
                        if (!string.IsNullOrEmpty(s) && seen.Add(s)) members.Add(s);
                        if (members.Count >= maxMembers) break;
                    }
                }
            }

            return members;
        }

        // ---- property helpers ----

        public static bool IsMemberOf(SearchResult r, string exactGroupCn)
        {
            if (!r.Properties.Contains("memberOf")) return false;

            var searchTarget = $"CN={exactGroupCn},";

            foreach (var v in r.Properties["memberOf"])
            {
                if (v?.ToString()?.Contains(searchTarget, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }
            return false;
        }

        public static string? GetString(SearchResult r, string prop)
        {
            return (r.Properties.Contains(prop) && r.Properties[prop].Count > 0)
                ? r.Properties[prop][0]?.ToString()
                : null;
        }

        public static IReadOnlyList<string> GetStrings(SearchResult r, string prop)
        {
            if (!r.Properties.Contains(prop) || r.Properties[prop].Count == 0) return [];
            return [.. r.Properties[prop].Cast<object>().Select(o => o?.ToString() ?? string.Empty)];
        }

        public static bool GetEnabledFromUac(SearchResult r)
        {
            if (!r.Properties.Contains("userAccountControl") || r.Properties["userAccountControl"].Count == 0) return false;
            var uac = Convert.ToInt32(r.Properties["userAccountControl"][0]);
            const int ACCOUNTDISABLE = 0x2;
            return (uac & ACCOUNTDISABLE) == 0;
        }

        // Quick lockout check, checks bit value only, not time-based policies
        public static bool? GetLockedQuick(SearchResult r)
        {
            const int UF_LOCKOUT = 0x0010;
            if (r.Properties.Contains("msDS-User-Account-Control-Computed") &&
                r.Properties["msDS-User-Account-Control-Computed"].Count > 0)
            {
                var v = Convert.ToInt32(r.Properties["msDS-User-Account-Control-Computed"][0]);
                return (v & UF_LOCKOUT) == UF_LOCKOUT;
            }
            return null;
        }

        // ---- internals ----
        public static DirectoryEntry Bind(string ldap) => new(ldap);

        private static DirectorySearcher NewSearcher(DirectoryEntry root, string filter, int sizeLimit, int pageSize = 0, string[]? props = null)
        {
            var ds = new DirectorySearcher(root)
            {
                Filter = filter,
                SearchScope = SearchScope.Subtree,
                CacheResults = true,
                ServerTimeLimit = TimeSpan.FromSeconds(15),
                SizeLimit = sizeLimit,
                PageSize = pageSize,
                ReferralChasing = ReferralChasingOption.None
            };
            if (props != null) foreach (var p in props) ds.PropertiesToLoad.Add(p);
            return ds;
        }

        // RFC2254 escaping for \ * ( ) NUL
        private static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                sb.Append(c switch
                {
                    '\\' => "\\5c",
                    '*' => "\\2a",
                    '(' => "\\28",
                    ')' => "\\29",
                    '\0' => "\\00",
                    _ => c
                });
            }
            return sb.ToString();
        }

        [GeneratedRegex(@"member;range=(\d+)-(\d+|\*)", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex DirectoryUtilRegex();
    }
}