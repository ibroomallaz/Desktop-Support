using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    Task<string?> GetTeamNameAsync(string departmentNumber, int teamNumber);
    Task<bool?> IsSupportKnownAsync(string departmentNumber);
    Task<string?> GetNotesAsync(string departmentNumber);
    Task<bool> HasFileRepoAsync(string departmentNumber);
    Task<FileRepo?> GetFileRepoAsync(string departmentNumber);
    Task PrecacheDataAsync();
}

public class DepartmentService : IDepartmentService
{
    private List<IDepartment>? _departments;
    private readonly SemaphoreSlim _lock = new(1, 1);  //Thread lock

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

    public async Task<string?> GetTeamNameAsync(string departmentNumber, int teamNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        // Return the name of the (teamNumber - 1)th team if available
        if (department?.Teams != null && department.Teams.Count >= teamNumber)
            return department.Teams[teamNumber - 1].Name;
        return null;
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
            if (_departments != null) return;

            if (!File.Exists(Globals.g_DepartmentJSONPath))
            {
                Console.WriteLine("Department data missing. Attempting to redownload...");
                await DownloadDepartmentData();
            }

            using StreamReader reader = new(Globals.g_DepartmentJSONPath);
            string json = await reader.ReadToEndAsync();

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

    private async Task DownloadDepartmentData()
    {
        try
        {
            string directoryPath = Path.GetDirectoryName(Globals.g_DepartmentJSONPath) ?? "";
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await HTTP.DownloadFile(Globals.g_DepartmentJSONURL, Globals.g_DepartmentJSONPath);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error downloading department data: {e}");
        }
    }
}
