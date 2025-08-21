using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;
using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;
using System.Diagnostics;

namespace DSAMVVM.Core.Services
{
    public class DepartmentService(IHttpService http) : IDepartmentService
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly SemaphoreSlim _lock = new(1, 1);

        // Cached, app-facing list (adapters implementing IDepartment)
        private List<IDepartment>? _departments;

        // Meta is kept if you want to surface schema/timestamp later
        private DepartmentMeta? _meta;

        public async Task PreCacheDataAsync() => await EnsureDataLoaded();

        public async Task<IDepartment?> GetDepartmentAsync(string departmentNumber)
        {
            await EnsureDataLoaded();
            return _departments?.FirstOrDefault(d => d.Number == departmentNumber);
        }

        public async Task<string?> GetTeamAsync(string departmentNumber)
            => (await GetDepartmentAsync(departmentNumber))?.Team;

        public async Task<string?> GetNotesAsync(string departmentNumber)
            => (await GetDepartmentAsync(departmentNumber))?.Notes;

        public async Task<bool?> IsSupportKnownAsync(string departmentNumber)
            => (await GetDepartmentAsync(departmentNumber))?.SupportKnown;

        public async Task<string?> GetFileRepoPathAsync(string departmentNumber)
            => (await GetDepartmentAsync(departmentNumber))?.FileRepoPath;

        public async Task ReloadDataAsync()
        {
            // Force reload regardless of cache state
            await _lock.WaitAsync();
            try { await LoadDepartmentsInternalAsync(isReload: true); }
            finally { _lock.Release(); }
        }

        // internals

        private async Task EnsureDataLoaded()
        {
            // Lazy load on first access
            if (_departments == null) await LoadDepartmentsAsync();
        }

        private async Task LoadDepartmentsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_departments != null) return; // already loaded in the gap
                await LoadDepartmentsInternalAsync(isReload: false);
            }
            finally { _lock.Release(); }
        }

        private async Task LoadDepartmentsInternalAsync(bool isReload)
        {
            var baseKey = isReload ? "DeptData.Reload" : "DeptData.Load";
            var progressKey = UiNotify.ProgressOf(baseKey);

            // Non-sticky progress message while loading
            UiNotify.Progress(
                baseKey,
                isReload ? "Refreshing department data…" : "Loading department data…",
                priority: 0);

            var sw = Stopwatch.StartNew();
            try
            {
                // Fetch JSON
                string json = await _http.GetStringAsync(Globals.g_DepartmentJSONURL);

                // Deserialize
                var wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(json)
                              ?? new DepartmentListWrapper();

                // Ensure meta has a timestamp if payload omitted it
                wrapper.Meta?.Normalize();
                _meta = wrapper.Meta;

                // Map raw entries to the app-facing interface via adapters
                _departments = (wrapper.DepartmentList ?? new List<Department>())
                    .Select(d => new DepartmentAdapter(d))
                    .ToList<IDepartment>();

                sw.Stop();
                UiNotify.RemoveKey(progressKey);

                // Resolution message replaces any sticky from previous failures
                UiNotify.Success(
                    $"{(isReload ? "Refreshed" : "Loaded")} {_departments.Count} departments in {sw.ElapsedMilliseconds} ms.",
                    showStatusBar: true,
                    key: baseKey);
            }
            catch (Exception e)
            {
                sw.Stop();
                UiNotify.RemoveKey(progressKey);

                // Sticky with Retry (async)
                UiNotify.WarnWithLinks(
                    $"Failed to {(isReload ? "refresh" : "load")} department data: {e.Message}",
                    sticky: true,
                    priority: 3,
                    key: baseKey,
                    UiNotify.Link.Action("Retry", async () => await ReloadDataAsync(), "Try the download again"));
            }
        }

        // Adapter for IDepartment
        private sealed class DepartmentAdapter(Department source) : IDepartment
        {
            private readonly Department _source = source;

            public string Number => _source.Number;
            public bool SupportKnown => _source.SupportKnown;
            public string? Team => string.IsNullOrWhiteSpace(_source.Team) ? null : _source.Team.Trim();
            public string? Notes => _source.Notes;
            public string? FileRepoPath => string.IsNullOrWhiteSpace(_source.FileRepoPath) ? null : _source.FileRepoPath;
        }
    }
}
