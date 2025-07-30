using DSAMVVM.Core;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    public class DepartmentService(IStatusReporter status) : IDepartmentService
    {
        private List<IDepartment>? _departments;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly IStatusReporter _status = status ?? throw new ArgumentNullException(nameof(status));

        public async Task PreCacheDataAsync() => await EnsureDataLoaded();

        public async Task<IDepartment?> GetDepartmentAsync(string departmentNumber)
        {
            await EnsureDataLoaded();
            return _departments?.FirstOrDefault(d => d.Number == departmentNumber);
        }

        public async Task<string?> GetNotesAsync(string departmentNumber)
            => (await GetDepartmentAsync(departmentNumber))?.Notes;

        public async Task<bool> HasFileRepoAsync(string departmentNumber)
            => (await GetDepartmentAsync(departmentNumber))?.FileRepos?.Any(fr => fr.Exists) ?? false;

        public async Task<FileRepo?> GetFileRepoAsync(string departmentNumber)
            => (await GetDepartmentAsync(departmentNumber))?.FileRepos?.FirstOrDefault(fr => fr.Exists);

        public async Task<List<string>> GetTeamNamesAsync(string departmentNumber)
        {
            var department = await GetDepartmentAsync(departmentNumber);
            List<string> teamNames = [];

            if (department?.Teams == null || department.Teams.Count == 0)
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
            => (await GetDepartmentAsync(departmentNumber))?.SupportKnown;

        private async Task LoadDepartmentsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_departments != null)
                    return;

                await LoadDepartmentsInternalAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task LoadDepartmentsInternalAsync()
        {
            try
            {
                using HttpClient client = new();
                string json = await client.GetStringAsync(Globals.g_DepartmentJSONURL);

                var wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(json);
                _departments = wrapper?.DepartmentList?
                    .Select(d => new DepartmentAdapter(d))
                    .ToList<IDepartment>() ?? [];

                _status.Report(StatusMessageFactory.Plain(
                    $"Loaded {_departments.Count} departments into memory.",
                    priority: 0, sticky: false, key: "DepartmentService"));
            }
            catch (Exception e)
            {
                _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                    $"Failed to load department data: {e.Message}. {{0}}",
                    [StatusMessageFactory.ActionLink("Retry", () => _ = ReloadDataAsync())],
                    priority: 3, sticky: true, key: "DepartmentService"));
            }
        }

        private async Task EnsureDataLoaded()
        {
            if (_departments == null)
                await LoadDepartmentsAsync();
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

        private class DepartmentAdapter(Department source) : IDepartment
        {
            private readonly Department _source = source;

            public string Number => _source.Number;
            public bool SupportKnown => _source.SupportKnown;
            public bool SplitSupport => _source.SplitSupport;
            public List<Team>? Teams => _source.Teams;
            public List<FileRepo>? FileRepos => _source.FileRepos;
            public string? Notes => _source.Notes;
        }
    }
}
