﻿using Colors.Net;
using Colors.Net.StringColorExtensions;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using static Colors.Net.StringStaticMethods;
class AD
{
    //Stack used for filerepo functionality
    public static Stack<string> adDeptStack = new Stack<string>();
    //TODO: further testing for speed on VPN. Connect to specific DCs etc.
    //global AD variable
    static readonly string domainPath = "LDAP://DC=bluecat,DC=arizona,DC=edu";
    public static void ADUser(string netid)
    {
        try
        {
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain))
            {
                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
                if (userPrincipal != null)
                {
                    adDeptStack.Clear(); //Clear stack of File repo functionality; Placed under ADUser function to account for typos
                    DirectoryEntry dirEntry = (DirectoryEntry)userPrincipal.GetUnderlyingObject();
                    string department = dirEntry.Properties["Department"].Value.ToString();
                    var displayName = userPrincipal.DisplayName.DarkYellow();
                    string eduAffiliation = dirEntry.Properties["eduPersonPrimaryAffiliation"].Value.ToString();
                    ColoredConsole.WriteLine($"{displayName}");
                    ColoredConsole.WriteLine($"{Cyan("Affiliation:")} {eduAffiliation.Red()}");
                    //Filter to find specific Division MIM group
                    using (DirectorySearcher searcher = new DirectorySearcher(AD.ConnectedServer))
                    {
                        searcher.Filter = $"(&(objectCategory=group)(member={userPrincipal.DistinguishedName})(cn=*MIM-DivisionRollup*))";
                        SearchResult result = searcher.FindOne();
                        if (result != null)
                        {
                            string groupName = result.Properties["cn"][0].ToString();
                            var division = groupName.Remove(4);
                            ColoredConsole.WriteLine($"{Cyan("Division: ")}" + division.Red());
                        }
                    }

                    if (department != null)
                    {
                        ColoredConsole.WriteLine($"{Cyan("Department:")} {department.Red()}");
                        CSV.PrintDepartmentInfo(department.Remove(4));
                        //for filerepo
                        adDeptStack.Push(department.Remove(4));
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
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    //TODO: test for methods without looping through
    public static void ADComputer(string hostname)
    {
        string searchFilter = $"(&(objectCategory=computer)(cn={hostname}))";
        try
        {
            DirectoryEntry entry = new DirectoryEntry(domainPath);
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
                    }
                }
                ColoredConsole.WriteLine($"{Cyan("Location:")} " + ous.Red());
                if (computer.Properties["description"].Value != null)
                {
                    string description = computer.Properties["description"].Value.ToString();
                    ColoredConsole.WriteLine($"{Cyan("Description:")} " + description.Red());
                }
                if (computer.Properties["OperatingSystem"].Value != null)
                {
                    ColoredConsole.WriteLine($"{Cyan("Operating System:")} " + computer.Properties["OperatingSystem"].Value.ToString());
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
    //TODO: test for methods that don't involve foreach loops to improve speed/efficiency
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
    //function to pull AD user from EmplID or StudentID
    public static void ADUserFromNumber(string userNumber)
    {
        using (DirectoryEntry entry = new DirectoryEntry(domainPath))
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
                        ColoredConsole.WriteLine(result.Properties["displayName"][0].ToString());
                    }
                    else
                    {
                        Console.WriteLine("Employee/StudentID not found.");
                    }
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }
}