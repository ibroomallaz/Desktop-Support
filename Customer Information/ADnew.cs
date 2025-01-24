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
            Dictionary<string, string> GetADUserResult = new Dictionary<string, string>();
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, domainPath))
            {
                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
                if (userPrincipal != null)
                {
                    GetADUserResult.Add("Name", netid);
                    DirectoryEntry dirEntry = (DirectoryEntry)userPrincipal.GetUnderlyingObject();
                    GetADUserResult.Add("Department", dirEntry.Properties["Department"].Value.ToString());
                    GetADUserResult.Add("DisplayName", userPrincipal.DisplayName);
                    GetADUserResult.Add("EduAffiliation", dirEntry.Properties["eduPersonPrimaryAffiliation"].Value.ToString());
                    GetADUserResult.Add("License", dirEntry.Properties["extensionattribute15"].Value.ToString());
                    //Filter to find specific Division MIM group
                    using (DirectorySearcher searcher = new DirectorySearcher(AD.ConnectedServer))
                    {
                        searcher.Filter = $"(&(objectCategory=group)(member={userPrincipal.DistinguishedName})(cn=*MIM-DivisionRollup*))";
                        SearchResult result = searcher.FindOne();
                        if (result != null)
                        {
                            string groupName = result.Properties["cn"][0].ToString();
                            GetADUserResult.Add("Division", groupName.Remove(4));
                        }
                    }
                }
                else
                {

                    throw new ArgumentException($"User {netid} not found.");
                }

            }
            this.Department = GetADUserResult["Department"];
            this.License = GetADUserResult["License"];
            this.EduAffiliation = GetADUserResult["EduAffiliation"];
            this.Division = GetADUserResult["Divison"];
        }
        catch (Exception ex)
        {
            this.Ex = ex.ToString();
        }



    }
 /*   Commenting out while messing with code
  *   
  *   public Dictionary<string, string> GetADUser(string netid)
    {

        try
        {
            Dictionary<string, string> GetADUserResult = new Dictionary<string, string>();
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, domainPath))
            {
                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
                if (userPrincipal != null)
                {
                    GetADUserResult.Add("Name", netid);
                    DirectoryEntry dirEntry = (DirectoryEntry)userPrincipal.GetUnderlyingObject();
                    GetADUserResult.Add("Department", dirEntry.Properties["Department"].Value.ToString());
                    GetADUserResult.Add("DisplayName", userPrincipal.DisplayName);
                    GetADUserResult.Add("EduAffiliation", dirEntry.Properties["eduPersonPrimaryAffiliation"].Value.ToString());
                    GetADUserResult.Add("License", dirEntry.Properties["extensionattribute15"].Value.ToString());
                    //Filter to find specific Division MIM group
                    using (DirectorySearcher searcher = new DirectorySearcher(AD.ConnectedServer))
                    {
                        searcher.Filter = $"(&(objectCategory=group)(member={userPrincipal.DistinguishedName})(cn=*MIM-DivisionRollup*))";
                        SearchResult result = searcher.FindOne();
                        if (result != null)
                        {
                            string groupName = result.Properties["cn"][0].ToString();
                            GetADUserResult.Add("Division", groupName.Remove(4));
                        }
                    }
                }
                else
                {

                    throw new ArgumentException($"User {netid} not found.");
                }

            }

            return GetADUserResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
 */
    }
}
    public class ADComputer(string name, string distinguishedName, string description)
	{
		public string name { get; set; }
		private string distinguishedName { get; set; }
		public string ous {  get; set; }
		public string description { get; set; }


    }
	public class ADGroup
	{
		public bool exists { get; set; }
	}

