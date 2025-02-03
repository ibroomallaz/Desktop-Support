using Colors.Net;
using Colors.Net.StringColorExtensions;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Xml.Linq;
using static Colors.Net.StringStaticMethods;

class ADUserInfo
{
    public string Name { get; set; }
    public string? Department { get; set; }
    public string? DisplayName { get; set; }
    public string? EduAffiliation { get; set; }
    public string? License { get; set; }
    public string? Division { get; set; }
    public string? Ex { get; set; }
    public static string UserFromNumber (string userNumber)
    {
        using (DirectoryEntry entry = new DirectoryEntry(Globals.g_domainPathLDAP))
        {
            using (DirectorySearcher searcher = new DirectorySearcher(entry))
            {

                searcher.Filter = $"(&(objectClass=user)(employeeID={userNumber}))";
                searcher.PropertiesToLoad.Add("displayName");
                searcher.PropertiesToLoad.Add("employeeID");

                try
                {
                    SearchResult result = searcher.FindOne();

                    if (result != null)
                    {
                        return result.Properties["displayName"][0].ToString();
                    }
                    else
                    {
                       return "Employee/StudentID not found.";
                    }

                }
                catch (Exception ex)
                {
                   return ex.Message;
                }
            }
        }
    }

    public ADUserInfo(string netid)

    {
        this.Name = netid;
        try
        {
            
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, Globals.g_domainPath))
            {
                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
                if (userPrincipal != null)
                {
                
                    DirectoryEntry dirEntry = (DirectoryEntry)userPrincipal.GetUnderlyingObject();
                   this.Department = dirEntry.Properties["Department"].Value.ToString();
                   this.DisplayName =  userPrincipal.DisplayName;
                    this.EduAffiliation = dirEntry.Properties["eduPersonPrimaryAffiliation"].Value.ToString();
                    this.License = dirEntry.Properties["extensionattribute15"].Value.ToString();
                    //Filter to find specific Division MIM group
                    using (DirectorySearcher searcher = new DirectorySearcher(AD.ConnectedServer))
                    {
                        searcher.Filter = $"(&(objectCategory=group)(member={userPrincipal.DistinguishedName})(cn=*MIM-DivisionRollup*))";
                        SearchResult result = searcher.FindOne();
                        if (result != null)
                        {
                            string groupName = result.Properties["cn"][0].ToString();
                            this.Division = groupName.Remove(4);
                        }
                    }
                }
                else
                {

                    throw new ArgumentException($"User {netid} not found.");
                }

            }
        }
        catch (Exception ex)
        {
            this.Ex = ex.ToString();
        }


    }

   
    }
    public class ADComputer
	{
    public string name { get; set; }
		private string? DistinguishedName { get; set; }
		public string? OUs {  get; set; }
		public string? Description { get; set; }
        public bool? IsHybridGroupMember { get; set; }
        public bool HybridGroup(DirectoryEntry computer) 
        {
        var memberOf = computer.Properties["memberOf"];
        if (memberOf != null)
        {
            // Loop through the list of groups the computer is a member of
            foreach (var group in memberOf)
            {
                if (group.ToString().Contains("UA-MEMHybridDevices", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        return false;
    }
        public string? OperatingSystem { get; set; }
        public string? Ex { get; set; }

        
        public ADComputer(string hostname)
             {
              string searchFilter = $"(&(objectCategory=computer)(cn={hostname}))";
        this.name = hostname;
        try
        {
            DirectoryEntry entry = new DirectoryEntry(Globals.g_domainPathLDAP);
            DirectorySearcher searcher = new DirectorySearcher(entry);
            searcher.Filter = searchFilter;
            SearchResult result = searcher.FindOne();
            if (result != null)
            {
                DirectoryEntry computer = result.GetDirectoryEntry();
                string distinguishedName = computer.Properties["distinguishedName"].Value.ToString();
                string[] dnParts = distinguishedName.Split(',');
                string ous = "";
                // Loop through the DN parts and find the OUs
                foreach (string dnPart in dnParts)
                {
                    if (dnPart.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(ous))
                            ous += ", ";
                        ous += dnPart;
                        this.OUs = ous;
                    }
                }
                if (computer.Properties["Description"].Value != null)
                {
                    this.Description = computer.Properties["description"].Value.ToString();

                }
                if (computer.Properties["OperatingSystem"].Value != null)
                {
                    this.OperatingSystem = computer.Properties["OperatingSystem"].Value.ToString();
                }
                
                else
                {
                    throw new ArgumentException($"Computer name {hostname} not found.");
                }
                this.IsHybridGroupMember = HybridGroup(computer);
            }
        }
        catch (Exception ex)
        {
            this.Ex = ex.ToString();
        }
    }
    }
	public class ADGroup
	{
		public bool exists { get; set; }
    public static void ADMIMGroupCheck(string netid)
    {
        try
        {
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, domainPath))
            {
                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
                if (userPrincipal != null)
                {
                    var groups = userPrincipal.GetGroups().ToArray();
                    if (groups != null)
                    {
                        Console.WriteLine("Current MIM Groups:");
                        //TODO: check on filter method instead of foreach loop
                        foreach (var group in groups)
                        {
                            if (group.Name.Contains("MIM"))
                            {
                                Console.WriteLine(group.Name);
                            }

                        }
                    }
                }
                if (userPrincipal == null)
                {
                    Console.WriteLine($"NetID {netid} does not exist.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    public static List<string> ADGroupMembers(string groupName)
    {
        List<string> groupMembers = new List<string>();
        try
        {

            using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain, domainPath))
            {
                using (GroupPrincipal grp = GroupPrincipal.FindByIdentity(ctx, IdentityType.Name, groupName))
                {
                    if (grp != null)
                    {
                        foreach (Principal p in grp.GetMembers())
                        {
                            groupMembers.Add(p.Name);
                            groupMembers.Sort();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No group {groupName} exists");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        if (groupMembers.Count == 0)
        {
            groupMembers.Add("No group members exist.");
        }

        return groupMembers;
    }
    public static bool ADGroupExistsCheck(string groupName)
    {
        using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, domainPath))
        {
            GroupPrincipal groupPrincipal = GroupPrincipal.FindByIdentity(AD, groupName);
            if (groupPrincipal != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}

