using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.IO;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;
using DSAMVVM.MVVM.Model.Schemas;
using System.Diagnostics;
using System.Linq;

namespace DSAMVVM.Core.Services
{
    // Remote-first with local JSON fallback + conditional backup refresh.
    public class DepartmentService(IHttpService http) : IDepartmentService
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly SemaphoreSlim _lock = new(1, 1);                     // single-flight load/reload
        private readonly JsonFileCache<DepartmentListWrapper> _fileCache =
            new(Globals.g_DepartmentCachePath);

        private List<IDepartment>? _departments;
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
            await _lock.WaitAsync();
            try { await LoadDepartmentsInternalAsync(isReload: true); }
            finally { _lock.Release(); }
        }

        private async Task EnsureDataLoaded()
        {
            if (_departments == null) await LoadDepartmentsAsync();
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

        private async Task LoadDepartmentsInternalAsync(bool isReload)
        {
            var key = isReload ? "DeptData.Reload" : "DeptData.Load";
            var progressKey = UiNotify.ProgressOf(key);
            UiNotify.Progress(key, isReload ? "Refreshing department data…" : "Loading department data…", priority: 0);

            var sw = Stopwatch.StartNew();
            try
            {
                // Loader: web-first; write backup when web stamp is newer (or no local); fallback to local on web failure.
                var wrapper = await RemoteWithBackUpLoader.LoadAsync(
                    _http,
                    Globals.g_DepartmentJSONURL,
                    _fileCache,
                    StampSelector,
                    ct: default,
                    jsonSettings: null,
                    normalize: w => w.Meta?.Normalize(),
                    log: msg => Log.Info("Dept.Loader", msg));

                if (wrapper == null)
                    throw new InvalidOperationException("No department data available from web or local cache.");

                _meta = wrapper.Meta;
                _departments = (wrapper.DepartmentList ?? new List<Department>())
                    .Select(d => new DepartmentAdapter(d))
                    .ToList<IDepartment>();

                sw.Stop();
                UiNotify.RemoveKey(progressKey);
                UiNotify.Success($"{(isReload ? "Refreshed" : "Loaded")} {_departments.Count} departments in {sw.ElapsedMilliseconds} ms.",
                                 showStatusBar: true, key: key);
            }
            catch (Exception e)
            {
                sw.Stop();
                UiNotify.RemoveKey(progressKey);
                UiNotify.WarnWithLinks(
                    $"Failed to {(isReload ? "refresh" : "load")} department data: {e.Message}",
                    sticky: true, priority: 3, key: key,
                    UiNotify.Link.Action("Retry", async () => await ReloadDataAsync(), "Try the download again"));
            }
        }

        // UTC stamp used for backup refresh decisions.
        private static DateTime? StampSelector(DepartmentListWrapper w) => w.Meta?.LastUpdatedUtc;

        // Thin adapter to keep UI decoupled from transport DTOs.
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
