using Colors.Net;
using Colors.Net.StringColorExtensions;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Xml.Linq;
using static Colors.Net.StringStaticMethods;

class ADUserInfo
{
    static readonly string domainPath = "bluecat.arizona.edu";
    static readonly string domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";
    public string Name { get; set; }
    public string? Department { get; set; }
    public string? DisplayName { get; set; }
    public string? EduAffiliation { get; set; }
    public string? License { get; set; }
    public string? Division { get; set; }
    public string? Ex { get; set; }
    public ADUserInfo(string netid)

    {
        this.Name = netid;
        try
        {
            
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, domainPath))
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
       
        static readonly string domainPath = "bluecat.arizona.edu";
        static readonly string domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";
    public string name { get; set; }
		private string? DistinguishedName { get; set; }
		public string? OUs {  get; set; }
		public string? Description { get; set; }
        public bool? HybridGroup { get; set; }
        public string? OperatingSystem { get; set; }
        public string? Ex { get; set; }
        
        public ADComputer(string hostname)
             {
              string searchFilter = $"(&(objectCategory=computer)(cn={hostname}))";
        this.name = hostname;
        try
        {
            DirectoryEntry entry = new DirectoryEntry(domainPathLDAP);
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
                ColoredConsole.Write($"{Cyan("Hybrid Join Group:")}");
                if (ADComputerHybridGroupCheck(computer))
                {
                    ColoredConsole.Write($"{Green(" Yes")}\n");
                }
                else
                {
                    ColoredConsole.Write($"{Red(" No")}\n");
                }
            }
            else
            {
                throw new ArgumentException($"Computer name {hostname} not found.");
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

	}

