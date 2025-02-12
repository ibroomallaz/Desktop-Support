﻿using Colors.Net;
using Colors.Net.StringColorExtensions;
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Colors.Net.StringStaticMethods;
using Microsoft.Extensions.DependencyInjection;
public class ADUserInfo
{
    public string Name { get; set; }
    public string? DepartmentName { get; set; }
    public string? DepartmentNumber { get; set; }
    public string? DisplayName { get; set; }
    public string? EduAffiliation { get; set; }
    public string? License { get; set; }
    public string? Division { get; set; }
    public string? Ex { get; set; }
    public bool Exists { get; set; }
    public static string UserFromNumber(string userNumber)
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
                    SearchResult? result = searcher.FindOne();

                    if (result != null)
                    {
                        return result?.Properties["displayName"][0].ToString() ?? "Unknown";
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
                    this.Exists = true;
                    DirectoryEntry dirEntry = (DirectoryEntry)userPrincipal.GetUnderlyingObject();
                    this.DepartmentName = dirEntry.Properties[nameof(Department)]?.Value?.ToString() ?? "None";
                    this.DepartmentNumber = this.DepartmentName.Substring(0, 4);
                    this.DisplayName = userPrincipal.DisplayName ?? "Unknown";
                    this.EduAffiliation = dirEntry.Properties["eduPersonPrimaryAffiliation"]?.Value?.ToString() ?? "Unknown";
                    this.License = _ADUserLicCheck(dirEntry.Properties["extensionattribute15"]?.Value?.ToString() ?? "Unlicensed");
                    //Filter to find specific Division MIM group
                    using (DirectorySearcher searcher = new DirectorySearcher(AD.ConnectedServer))
                    {
                        searcher.Filter = $"(&(objectCategory=group)(member={userPrincipal.DistinguishedName})(cn=*MIM-DivisionRollup*))";
                        SearchResult? result = searcher.FindOne();
                        if (result?.Properties["cn"]?.Count > 0)
                        {
                            string? groupName = result.Properties["cn"][0]?.ToString();
                            this.Division = groupName?.Length >= 4 ? groupName.Substring(0, 4) : "N/A";
                        }
                    }
                }
                else
                {

                    this.Exists = false;
                }

            }
        }
        catch (Exception ex)
        {
            this.Ex = ex.ToString();
        }


    }
    public List<string>? ADMIMGroupCheck(string netid) 
{
    List<string> mimGroups = new List<string>();
    try
    {
        using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, Globals.g_domainPath))
        {
            UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
            if (userPrincipal != null)
            {
                this.Exists = true;
                mimGroups = userPrincipal.GetGroups()?
                                       .Where(group => group.Name.Contains("MIM"))
                                       .Select(group => group.Name)
                                       .ToList() ?? new List<string>();

                return mimGroups;
            }  
            this.Exists = false;
        }
    }
    catch (Exception ex)
    {
        this.Ex = ex.ToString();
    }
    return null;
}
    private string _ADUserLicCheck(string license)   //Thanks to Danton
    {
        string p = "([om]{1}\\d{3})([A-Z]+)([AE]\\d{1})";
        Match match = Regex.Match(license, p);

        string group1 = "", group2 = "", group3 = "";

        if (match.Success)
        {
            group1 = match.Groups[1].Value;
            group2 = match.Groups[2].Value;
            group3 = match.Groups[3].Value;
        }
        else
        {
            string[] la = license.Split('(', ')');
            foreach (string l in la)
            {
                if (l.Contains("365"))
                {
                    return l + " (Unknown License Type)"; 
                }
            }
            return "No valid O365 license found"; 
        }

        string res = "";
        switch (group2.ToLower())
        {
            case "stuw":
                res = "Student Worker";
                break;
            case "emp":
                res = "Employee";
                break;
            case "stu":
                res = "Student";
                break;
            default:
                res = "Unknown";
                break;
        }

        return $"{res} {group3}";
    }
    public static async void PrintADUserInfo(ADUserInfo ADUser)
    {
        Console.WriteLine(); // For asthetics
        ColoredConsole.WriteLine(ADUser.DisplayName.DarkYellow());
        if (!string.IsNullOrEmpty(ADUser.EduAffiliation))
        {
            ColoredConsole.Write($"{Cyan("Affiliation: ")}");
            ColoredConsole.WriteLine(ADUser.EduAffiliation.Red());
        }
        if (!string.IsNullOrEmpty(ADUser.Division))
        {
            ColoredConsole.Write($"{Cyan("Division: ")}");
            ColoredConsole.WriteLine(ADUser.Division.Red());
        }

        if (!string.IsNullOrEmpty(ADUser.DepartmentName))
        {
            ColoredConsole.Write($"{Cyan("Department: ")}");
            ColoredConsole.WriteLine(ADUser.DepartmentName.Red());
        }

        ColoredConsole.Write($"{Cyan("O365 Licensing: ")}");
        ColoredConsole.WriteLine(ADUser.License.Red());

        // Use DepartmentNumber to search in the cached department data.
        if (!string.IsNullOrEmpty(ADUser.DepartmentNumber))
        {
            var department = await Globals.DepartmentService.GetDepartmentAsync(ADUser.DepartmentNumber);

            if (department != null)
            {
                if (department.Teams != null && department.Teams.Any())
                {
                    var serviceNowTeams = department.Teams.Where(t => t.ServiceNow).ToList();
                    if (serviceNowTeams.Any())
                    {
                        ColoredConsole.Write($"{Cyan("Service Now Team: ")}");
                        ColoredConsole.WriteLine(string.Join(", ", serviceNowTeams.Select(t => t.Name)).Red());
                    }
                    else
                    {
                        ColoredConsole.Write($"{Cyan("Support Team: ")}");
                        ColoredConsole.WriteLine(string.Join(", ", department.Teams.Select(t => t.Name)).Red());
                    }
                }
                else
                {
                    ColoredConsole.WriteLine(Cyan("Teams: ") + "None".Red());
                }

                // Check for and print file repository details.
                bool hasRepo = await Globals.DepartmentService.HasFileRepoAsync(ADUser.DepartmentNumber);
                if (hasRepo)
                {
                    var fileRepo = await Globals.DepartmentService.GetFileRepoAsync(ADUser.DepartmentNumber);
                    if (fileRepo != null)
                    {
                        ColoredConsole.Write($"{Cyan("File Repository: ")}");
                        ColoredConsole.WriteLine(fileRepo.Location.Red());
                    }
                    else
                    {
                        ColoredConsole.WriteLine(Cyan("File Repository: ") + "Details unavailable".Red());
                    }
                }
                if (!string.IsNullOrEmpty(department.Notes))
                {
                    ColoredConsole.Write($"{Cyan("Notes: ")}");
                    ColoredConsole.WriteLine(department.Notes.Red());
                }
            }
            else
            {
                ColoredConsole.WriteLine("Department information not found in cache.".Red());
            }
        }
        Console.WriteLine(); //Asthetics again
    }





}

public class ADComputer
{
    public string name { get; set; }
    private string? DistinguishedName { get; set; }
    public string? OUs { get; set; }
    public string? Description { get; set; }
    public bool IsHybridGroupMember { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Ex { get; set; }
    public bool Exists { get; set; }
    private bool _HybridGroup(DirectoryEntry computer)
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


    public ADComputer(string hostname)
    {
        string searchFilter = $"(&(objectCategory=computer)(cn={hostname}))";
        this.name = hostname;
        try
        {
            DirectoryEntry entry = new DirectoryEntry(Globals.g_domainPathLDAP);
            DirectorySearcher searcher = new DirectorySearcher(entry);
            searcher.Filter = searchFilter;
            SearchResult? result = searcher.FindOne();
            if (result != null)
            {
                this.Exists = true;
                DirectoryEntry computer = result.GetDirectoryEntry();
                string? distinguishedName = computer.Properties["distinguishedName"].Value?.ToString() ?? null;
                string[]? dnParts = distinguishedName.Split(',');
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
                if (computer.Properties[nameof(Description)].Value != null)
                {
                    this.Description = computer.Properties["description"].Value?.ToString() ?? null;

                }
                if (computer.Properties[nameof(OperatingSystem)].Value != null)
                {
                    this.OperatingSystem = computer.Properties[nameof(OperatingSystem)].Value?.ToString() ?? "Unknown";
                }

                else
                {
                    this.Exists = false;
                    throw new ArgumentException($"Computer name {hostname} not found.");
                }
                this.IsHybridGroupMember = _HybridGroup(computer);
            }
        }
        catch (Exception ex)
        {
            this.Ex = ex.ToString();
        }
    }
    public static async Task PrintADComputerInfo(ADComputer computer)
    {

        Console.WriteLine();
        ColoredConsole.Write($"{Cyan("Location: ")}");
        ColoredConsole.WriteLine(computer.OUs.Red());
        if (!string.IsNullOrEmpty(computer.Description))
        {
            ColoredConsole.Write($"{Cyan("Description: ")}");
            ColoredConsole.WriteLine(computer.Description.Red());
        }
        if (!string.IsNullOrEmpty(computer.OperatingSystem))
        {
            ColoredConsole.Write($"{Cyan("Operating System: ")}");
            ColoredConsole.WriteLine(computer.OperatingSystem.Red());
        }
        ColoredConsole.Write($"{Cyan("Hybrid Join Group: ")}");
        if (computer.IsHybridGroupMember)
        {
            ColoredConsole.WriteLine($"{Green("Yes")}");
        }
        else
        {
            ColoredConsole.WriteLine($"{Red("No")}");
        }
        Console.WriteLine();
    }
}
public class ADGroup
{
    public bool Exists { get; set; }
    public ADGroup(string groupname)
    {
        this.Exists = _ADGroupExistsCheck(groupname);
    }

    public List<string> ADGroupMembers(string groupName)
    {
        List<string> groupMembers = new List<string>();
        try
        {

            using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain, Globals.g_domainPath))
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
                        throw new ArgumentException($"No group {groupName} exists");
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
    private static bool _ADGroupExistsCheck(string groupName)
    {
        using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, Globals.g_domainPath))
        {
            GroupPrincipal groupPrincipal = GroupPrincipal.FindByIdentity(AD, groupName);
            if (groupPrincipal != null)
            {
                return true;
            }
            return false;
        }

    }
}

