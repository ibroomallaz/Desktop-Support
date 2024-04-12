using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using Colors.Net;
using static Colors.Net.StringStaticMethods;
using Colors.Net.StringColorExtensions;
class AD
{
    //#fr
    public static Stack<string> adDeptStack = new Stack<string>();
    public static void ADUser(string netid)
    {
        try
        {
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain))
            {
                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
                if (userPrincipal != null)
                {
                    DirectoryEntry dirEntry = (DirectoryEntry)userPrincipal.GetUnderlyingObject();
                    string department = dirEntry.Properties["Department"].Value.ToString();
                    var displayName = userPrincipal.DisplayName.DarkYellow();
                    ColoredConsole.WriteLine($"{displayName}");



                    foreach (GroupPrincipal group in userPrincipal.GetGroups())
                    {
                        if (group.Name.Contains("MIM-DivisionRollup"))
                        {
                            var division = group.Name.Remove(4).Red();
                            ColoredConsole.WriteLine($"{Cyan("Division: ")}" + division);
                        }
                    }
                    if (department != null)
                    {
                        ColoredConsole.WriteLine($"{Cyan("Department:")} {department.Red()}");
                        CSV.PrintDepartmentInfo(department.Remove(4));
                        //#fr
                        adDeptStack.Push(department);
                    }
                    if (department == null)
                    {
                        Console.WriteLine("Not a part of a Department");
                    }
                }
                else
                {
                    Console.WriteLine($"User {netid} not found.");
                }
            }
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error: {ex.Message}");   disabling for now
        }
    }

    public static void ADComputer(string hostname)
    {
        string domainPath = "LDAP://DC=bluecat,DC=arizona,DC=edu";
        string searchFilter = $"(&(objectCategory=computer)(cn={hostname}))";
        try
        {
            DirectoryEntry entry = new DirectoryEntry(domainPath);
            DirectorySearcher searcher = new DirectorySearcher(entry);
            searcher.Filter = searchFilter;
            SearchResult result = searcher.FindOne();
            //canonical name may be easier to read
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
                    }
                }
                ColoredConsole.WriteLine($"{Cyan("Location:")} " + ous.Red());
                if (computer.Properties["description"].Value != null)
                {
                    string description = computer.Properties["description"].Value.ToString();
                    ColoredConsole.WriteLine($"{Cyan("Description:")} " + description.Red());
                }


            }
            else
            {
                Console.WriteLine("No Device found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }
    }
    public static List<string> ADGroupMembers(string groupName)
    {
        List<string> groupMembers = new List<string>();
        try
        {
           
            using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain))
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
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain))
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
    public static void ADMIMGroupCheck(string netid)
    {
        try
        {
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain))
            {
                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
                if (userPrincipal != null)
                {
                    var groups = userPrincipal.GetGroups().ToArray();
                    if (groups != null)
                    {
                        Console.WriteLine("Current MIM Groups:");
                        foreach (var group in groups)
                        {
                            if (group.Name.Contains("MIM"))
                            {
                                Console.WriteLine(group.Name);
                            }
                           
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}