using Colors.Net;
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
    public string? ErrorMessage { get; set; }
    public bool Exists { get; set; }
    public bool? MimGroupExists { get; set; }
    public List<string>? MimGroupsList { get; set; }

    public static Task UserFromNumber(string userNumber)
    {
        using (DirectoryEntry entry = new DirectoryEntry(Globals.g_domainPathLDAP))
        using (DirectorySearcher searcher = new DirectorySearcher(entry))
        {
            searcher.Filter = $"(&(objectClass=user)(employeeID={userNumber}))";
            searcher.PropertiesToLoad.Add("displayName");
            searcher.PropertiesToLoad.Add("employeeID");

            try
            {
                SearchResult? result = searcher.FindOne();
                Console.WriteLine(result != null ? result.Properties["displayName"][0].ToString() ?? "Unknown" : "Employee/StudentID not found.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        return Task.CompletedTask;
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
        catch (PrincipalServerDownException)
        {
            this.ErrorMessage = "Unable to connect to the domain controller.";
            this.Exists = false;
        }
        catch (Exception ex)
        {
            this.ErrorMessage = $"Unexpected error during AD lookup: {ex}";
            this.Exists = false;
        }
    }

    public List<string>? GetADMIMGroups(string netid)
    {
        List<string> mimGroups = new();
        try
        {
            using (PrincipalContext AD = new PrincipalContext(ContextType.Domain, Globals.g_domainPath))
            {
                UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(AD, netid);
                if (userPrincipal != null)
                {
                    mimGroups = userPrincipal.GetGroups()?
                        .Where(group => group.Name.Contains("MIM"))
                        .Select(group => group.Name)
                        .ToList() ?? new();
                    this.MimGroupsList = mimGroups;
                    this.MimGroupExists = mimGroups.Count > 0;
                    return mimGroups;
                }
            }
        }
        catch (Exception ex)
        {
            this.ErrorMessage = $"Error retrieving MIM groups: {ex}";
        }
        return null;
    }

    private string _ADUserLicCheck(string license)
    {
        string p = "([om]{1}\\d{3})([A-Z]+)([AE]\\d{1})";
        Match match = Regex.Match(license, p);

        if (match.Success)
        {
            string group2 = match.Groups[2].Value;
            string group3 = match.Groups[3].Value;
            string res = group2.ToLower() switch
            {
                "stuw" => "Student Worker",
                "emp" => "Employee",
                "stu" => "Student",
                _ => "Unknown"
            };
            return $"{res} {group3}";
        }

        foreach (var l in license.Split('(', ')'))
        {
            if (l.Contains("365"))
                return l + " (Unknown License Type)";
        }

        return "No valid O365 license found";
    }

    public static async Task PrintADUserInfo(ADUserInfo ADUser)
    {
        Console.WriteLine();
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

        if (!string.IsNullOrEmpty(ADUser.DepartmentNumber))
        {
            var department = await Globals.DepartmentService.GetDepartmentAsync(ADUser.DepartmentNumber);

            if (department != null)
            {
                var serviceNowTeams = department.Teams?.Where(t => t.ServiceNow).ToList();
                if (serviceNowTeams?.Any() == true)
                {
                    ColoredConsole.Write($"{Cyan("Service Now Team: ")}");
                    ColoredConsole.WriteLine(string.Join(", ", serviceNowTeams.Select(t => t.Name)).Red());
                }
                else if (department.Teams?.Any() == true)
                {
                    ColoredConsole.Write($"{Cyan("Support Team: ")}");
                    ColoredConsole.WriteLine(string.Join(", ", department.Teams.Select(t => t.Name)).Red());
                }
                else
                {
                    ColoredConsole.WriteLine(Cyan("Teams: ") + "None".Red());
                }

                if (await Globals.DepartmentService.HasFileRepoAsync(ADUser.DepartmentNumber))
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

        Console.WriteLine();
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
    public string? ErrorMessage { get; set; }
    public bool Exists { get; set; }

    public ADComputer(string hostname)
    {
        this.name = hostname;
        string searchFilter = $"(&(objectCategory=computer)(cn={hostname}))";

        try
        {
            using (DirectoryEntry entry = new DirectoryEntry(Globals.g_domainPathLDAP))
            using (DirectorySearcher searcher = new DirectorySearcher(entry))
            {
                searcher.Filter = searchFilter;
                SearchResult? result = searcher.FindOne();
                if (result != null)
                {
                    this.Exists = true;
                    using (DirectoryEntry computer = result.GetDirectoryEntry())
                    {
                        string? dn = computer.Properties["distinguishedName"].Value?.ToString();
                        if (dn != null)
                        {
                            this.OUs = string.Join(", ", dn.Split(',').Where(p => p.StartsWith("OU=")));
                        }

                        this.Description = computer.Properties["description"]?.Value?.ToString();
                        this.OperatingSystem = computer.Properties["operatingSystem"]?.Value?.ToString() ?? "Unknown";
                        this.IsHybridGroupMember = _HybridGroup(computer);
                    }
                }
                else
                {
                    this.Exists = false;
                    this.ErrorMessage = $"Computer name {hostname} not found.";
                }
            }
        }
        catch (DirectoryServicesCOMException)
        {
            this.ErrorMessage = "Unable to connect to the domain controller.";
            this.Exists = false;
        }
        catch (Exception ex)
        {
            this.ErrorMessage = $"Unexpected error during AD computer lookup: {ex}";
            this.Exists = false;
        }
    }

    private bool _HybridGroup(DirectoryEntry computer)
    {
        var memberOf = computer.Properties["memberOf"];
        if (memberOf == null) return false;

        foreach (var group in memberOf)
        {
            if (group?.ToString()?.Contains("UA-MEMHybridDevices", StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }
        return false;
    }

    public static Task PrintADComputerInfo(ADComputer computer)
    {
        Console.WriteLine();
        ColoredConsole.Write($"{Cyan("Location: ")}");
        ColoredConsole.WriteLine(computer.OUs?.Red());

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
        ColoredConsole.WriteLine(computer.IsHybridGroupMember ? Green("Yes") : Red("No"));

        Console.WriteLine();
        return Task.CompletedTask;
    }
}

public class ADGroup
{
    public bool Exists { get; private set; }
    public List<string>? GroupMembers { get; private set; }
    public int? MemberCount { get; private set; }
    public string? ErrorMessage { get; private set; }

    public ADGroup(string groupName)
    {
        GroupSearch(groupName);
    }

    private void GroupSearch(string groupName)
    {
        try
        {
            using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain, Globals.g_domainPath))
            using (GroupPrincipal grp = GroupPrincipal.FindByIdentity(ctx, IdentityType.Name, groupName))
            {
                if (grp != null)
                {
                    Exists = true;
                    GroupMembers = grp.GetMembers()
                        .Select(p => p.Name)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToList();

                    GroupMembers.Sort();
                    MemberCount = GroupMembers.Count;

                    if (MemberCount == 0)
                        GroupMembers.Add("No group members exist.");
                }
                else
                {
                    Exists = false;
                    GroupMembers = new List<string> { "Group does not exist." };
                    MemberCount = 0;
                }
            }
        }
        catch (PrincipalServerDownException)
        {
            Exists = false;
            ErrorMessage = "Unable to connect to the domain controller.";
            GroupMembers = new List<string> { ErrorMessage };
            MemberCount = 0;
        }
        catch (Exception ex)
        {
            Exists = false;
            ErrorMessage = $"Error retrieving group: {ex}";
            GroupMembers = new List<string> { ErrorMessage };
            MemberCount = 0;
        }
    }
}
