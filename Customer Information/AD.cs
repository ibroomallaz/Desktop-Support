﻿using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using Colors.Net;
using static Colors.Net.StringStaticMethods;
using Colors.Net.StringColorExtensions;
class AD
{

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
}