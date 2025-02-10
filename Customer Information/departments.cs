using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;


public class DepartmentListWrapper
{
    public List<Department>? DepartmentList { get; set; }
}

public interface IDepartment
{
    string Number { get; }
    bool SupportKnown { get; }
    bool SplitSupport { get; }
    List<Team>? Teams { get; }
    List<FileRepo>? FileRepos { get; }
    string? Notes { get; }

}

public class Department : IDepartment
{
    public string Number { get; set; } = string.Empty;
    public bool SupportKnown { get; set; }
    public bool SplitSupport { get; set; }
    public List<Team>? Teams { get; set; }
    [JsonProperty("FileRepo")]
    public List<FileRepo>? FileRepos { get; set; }
    public string? Notes { get; set; }
}

public class Team
{
    public string Name { get; set; } = string.Empty;
    public bool ServiceNow { get; set; }
}

public class FileRepo
{
    public bool Exists { get; set; }
    public string? Location { get; set; }
}


public interface IDepartmentService
{
    Task<IDepartment?> GetDepartmentAsync(string departmentNumber);
    Task<List<string>> GetTeamNamesAsync(string departmentNumber);
    Task<bool?> IsSupportKnownAsync(string departmentNumber);
    Task<string?> GetNotesAsync(string departmentNumber);
    Task<bool> HasFileRepoAsync(string departmentNumber);
    Task<FileRepo?> GetFileRepoAsync(string departmentNumber);
    Task PrecacheDataAsync();
    Task PrintDepartmentAsync(string departmentNumber);
}


public class DepartmentService : IDepartmentService
{
    private List<IDepartment>? _departments;
    private readonly SemaphoreSlim _lock = new(1, 1);  //Thread lock
    private readonly HttpClient _httpClient;

    public DepartmentService()
    {
        _ = LoadDepartmentsAsync();
    }

    public async Task PrecacheDataAsync()
    {
        await EnsureDataLoaded();
    }

    public async Task<IDepartment?> GetDepartmentAsync(string departmentNumber)
    {
        await EnsureDataLoaded();
        return _departments?.FirstOrDefault(d => d.Number == departmentNumber);
    }

    public async Task<string?> GetNotesAsync(string departmentNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        return department?.Notes;
    }

    public async Task<bool> HasFileRepoAsync(string departmentNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        return department?.FileRepos?.Any(fr => fr.Exists) ?? false;
    }

    public async Task<FileRepo?> GetFileRepoAsync(string departmentNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        return department?.FileRepos?.FirstOrDefault(fr => fr.Exists);
    }

    public async Task<List<string>> GetTeamNamesAsync(string departmentNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        List<string> teamNames = new();

        // If the department or its team list is null or empty, return the empty list.
        if (department == null || department.Teams == null || !department.Teams.Any())
            return teamNames;

        // If the department is split (i.e. supports two teams), then return at most two names.
        if (department.SplitSupport)
        {
            // If there is at least one team, add the first team's name.
            if (department.Teams.Count >= 1)
                teamNames.Add(department.Teams[0].Name);

            // If there is a second team, add it.
            if (department.Teams.Count >= 2)
                teamNames.Add(department.Teams[1].Name);
        }
        else
        {
            // For non‑split departments, just return the first team’s name.
            teamNames.Add(department.Teams[0].Name);
        }

        return teamNames;
    }

    public async Task<bool?> IsSupportKnownAsync(string departmentNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        return department?.SupportKnown;
    }

    private async Task LoadDepartmentsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_departments != null) return; // Already loaded
            using HttpClient client = new HttpClient();
            
            string json = await client.GetStringAsync(Globals.g_DepartmentJSONURL);


            var wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(json);
            _departments = wrapper?.DepartmentList?
                .Select(d => (IDepartment)d)
                .ToList() ?? new List<IDepartment>();

            Console.WriteLine($"Loaded {_departments.Count} departments into memory.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load department data: {e}");
            _departments = new List<IDepartment>();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureDataLoaded()
    {
        if (_departments == null)
        {
            await LoadDepartmentsAsync();
        }
    }

    public async Task PrintDepartmentAsync(string departmentNumber)
    {
        await EnsureDataLoaded();  // Ensure the department data is loaded first.

        var dept = await GetDepartmentAsync(departmentNumber);
        if (dept == null)
        {
            Console.WriteLine($"Department {departmentNumber} not found.");
            return;
        }

        // Debug: check if dept details are being printed
        Console.WriteLine($"Printing details for department {dept.Number}");

        Console.WriteLine($"Department: {dept.Number}");
        Console.WriteLine($"  Support Known: {dept.SupportKnown}");
        Console.WriteLine($"  Split Support: {dept.SplitSupport}");

        // Debug output for team count
        Console.WriteLine($"  Teams count: {dept.Teams?.Count ?? 0}");

        if (dept.Teams != null && dept.Teams.Any())
        {
            int index = 1;
            foreach (var team in dept.Teams)
            {
                Console.WriteLine($"  Team {index}: {team.Name} (ServiceNow: {team.ServiceNow})");
                index++;
            }
        }
        else
        {
            Console.WriteLine("  No team information available.");
        }

        if (dept.FileRepos != null && dept.FileRepos.Any(fr => fr.Exists))
        {
            foreach (var repo in dept.FileRepos)
            {
                if (repo.Exists)
                {
                    Console.WriteLine($"  File Repo Location: {repo.Location}");
                }
            }
        }
        else
        {
            Console.WriteLine("  No file repository available.");
        }

        if (!string.IsNullOrEmpty(dept.Notes))
        {
            Console.WriteLine($"  Notes: {dept.Notes}");
        }

        Console.WriteLine(new string('-', 40));
    }


}
