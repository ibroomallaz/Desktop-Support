using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;

public interface IDepartmentService
{
    Task<IDepartment?> GetDepartmentAsync(string departmentNumber);
    Task<string?> GetTeamNameAsync(string departmentNumber, int teamNumber);
    Task<bool?> IsSupportKnownAsync(string departmentNumber);
    Task<string?> GetNotesAsync(string departmentNumber);
    Task<bool> HasFileRepoAsync(string departmentNumber);
    Task<FileRepo?> GetFileRepoAsync(string departmentNumber);
}
public class DepartmentService : IDepartmentService
{
    private List<IDepartment>? _departments;
    private readonly SemaphoreSlim _lock = new(1, 1); //Thread lock

    public DepartmentService() //load into memory
    {
        _ = LoadDepartmentsAsync();
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
        return department?.Filerepo?.Exists ?? false;
    }

    public async Task<FileRepo?> GetFileRepoAsync(string departmentNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        return department?.Filerepo?.Exists == true ? department.Filerepo : null;
    }

    public async Task<bool> IsDepartmentSplitAsync(string departmentNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        return department?.SplitSupport ?? false;
    }

    public async Task<string?> GetTeamNameAsync(string departmentNumber, int teamNumber)
    {
        var department = await GetDepartmentAsync(departmentNumber);
        return teamNumber switch
        {
            1 => department?.Team1?.Name,
            2 => department?.Team2?.Name,
            _ => null
        };
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

            _departments = JsonConvert.DeserializeObject<List<Department>>(json)?
                .Select(d => (IDepartment)d) // Ensure correct casting
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




public interface IDepartment
{
    string Number { get; }
    bool SupportKnown { get; }
    bool SplitSupport { get; }
    Team? Team1 { get; }
    Team? Team2 { get; }
    FileRepo? Filerepo { get; }
    string? Notes { get; }
}
public class Department : IDepartment
{
    public required string Number { get; set; }
    public bool SupportKnown { get; set; }
    public bool SplitSupport { get; set; }
    public Team? Team1 { get; set; }
    public Team? Team2 { get; set; }
    public FileRepo? Filerepo { get; set; }
    public string? Notes { get; set; }
}

public class Team
{
    public required string Name { get; set; }
    public bool ServiceNow { get; set; }
}

public class FileRepo
{
    public bool Exists { get; set; }
    public string? Location { get; set; }
}
