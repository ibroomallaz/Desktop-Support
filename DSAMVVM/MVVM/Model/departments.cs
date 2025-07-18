using DSAMVVM.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;


namespace DSAMVVM.MVVM.Model
{
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
        Task PreCacheDataAsync();
        Task ReloadDataAsync();
    }


    public class DepartmentService : IDepartmentService
    {
        private List<IDepartment>? _departments;
        private readonly SemaphoreSlim _lock = new(1, 1);  //Thread lock
        private readonly IStatusReporter _status;
        public DepartmentService(IStatusReporter status)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _ = LoadDepartmentsAsync();
        }

        public async Task PreCacheDataAsync()
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

            if (department == null || department.Teams == null || !department.Teams.Any())
                return teamNames;

            if (department.SplitSupport)
            {
                if (department.Teams.Count >= 1)
                    teamNames.Add(department.Teams[0].Name);
                if (department.Teams.Count >= 2)
                    teamNames.Add(department.Teams[1].Name);
            }
            else
            {
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
                if (_departments != null)
                    return; // Already loaded

                await LoadDepartmentsInternalAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task LoadDepartmentsInternalAsync()   //Load data without sempahor, helps prevent issues on -reaload command
        {
            try
            {
                using HttpClient client = new HttpClient();
                string json = await client.GetStringAsync(Globals.g_DepartmentJSONURL);

                var wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(json);
                _departments = wrapper?.DepartmentList?
                    .Select(d => (IDepartment)d)
                    .ToList() ?? new List<IDepartment>();

                _status.Report(StatusMessageFactory.Plain($"Loaded {_departments.Count} departments into memory.", 0));
            }
            catch (Exception e)
            {
                _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                    $"Failed to load department data: {e.Message}. {{0}}",
                    new Inline[]
                    {
                        StatusMessageFactory.ActionLink("Retry", () => _ = ReloadDataAsync())
                    },
                    priority: 3,
                    sticky: true
                ));
            }
        }

        private async Task EnsureDataLoaded()
        {
            if (_departments == null)
            {
                await LoadDepartmentsAsync();
            }
        }
        public async Task ReloadDataAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _departments = null;
                await LoadDepartmentsInternalAsync();
            }
            finally
            {
                _lock.Release();
            }
        }


    }
}
