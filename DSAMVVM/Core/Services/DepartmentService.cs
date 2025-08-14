// DSAMVVM.Core.Services/DepartmentService.cs
using DSAMVVM.Core;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;   // UiNotify + StatusMessageFactory
using DSAMVVM.MVVM.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;       // Stopwatch
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    // Uses shared IHttpService + UiNotify (no direct HttpClient, no IStatusReporter).
    public class DepartmentService(IHttpService http) : IDepartmentService
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly SemaphoreSlim _lock = new(1, 1);
        private List<IDepartment>? _departments;

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
                if (department.Teams.Count >= 1) teamNames.Add(department.Teams[0].Name);
                if (department.Teams.Count >= 2) teamNames.Add(department.Teams[1].Name);
            }
            else
            {
                teamNames.Add(department.Teams[0].Name);
            }

            return teamNames;
        }

        public async Task<bool?> IsSupportKnownAsync(string departmentNumber)
            => (await GetDepartmentAsync(departmentNumber))?.SupportKnown;

        // internals

        private async Task EnsureDataLoaded()
        {
            if (_departments == null)
                await LoadDepartmentsAsync();
        }

        private async Task LoadDepartmentsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_departments != null) return;
                await LoadDepartmentsInternalAsync(isReload: false);
            }
            finally { _lock.Release(); }
        }

        public async Task ReloadDataAsync()
        {
            await _lock.WaitAsync();
            try
            {
                await LoadDepartmentsInternalAsync(isReload: true);
            }
            finally { _lock.Release(); }
        }

        private async Task LoadDepartmentsInternalAsync(bool isReload)
        {
            const string key = "DepartmentService";

            // Start: sticky status so it's visible during work
            UiNotify.Push(StatusMessageFactory.Plain(
                isReload ? "Refreshing department data…" : "Loading department data…",
                priority: 0, sticky: true, key: key));

            var sw = Stopwatch.StartNew();
            try
            {
                // Shared HttpService, no direct HttpClient
                string json = await _http.GetStringAsync(Globals.g_DepartmentJSONURL);

                var wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(json);
                _departments = wrapper?.DepartmentList?
                    .Select(d => new DepartmentAdapter(d))
                    .ToList<IDepartment>() ?? [];

                sw.Stop();

                // Success: short, non-sticky toast on the status bar + log
                UiNotify.Success(
                    $"{(isReload ? "Refreshed" : "Loaded")} {_departments.Count} departments in {sw.ElapsedMilliseconds} ms.",
                    showStatusBar: true, key: key);
            }
            catch (Exception e)
            {
                sw.Stop();

                // Failure: log + rich status with a Retry action
                UiNotify.WarnWithLinks(
                    $"Failed to {(isReload ? "refresh" : "load")} department data: {e.Message}",
                    sticky: true, priority: 3, key: key,
                    UiNotify.Link.Action("Retry", () => ReloadDataAsync()));
            }
        }

        // Adapter keeps public surface aligned with IDepartment
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
