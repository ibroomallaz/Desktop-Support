using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.IO;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;
using DSAMVVM.MVVM.Model.Schemas;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

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

        // Class-level dictionary for fast Team lookups
        private Dictionary<string, SupportTeam> _teamMap = [];

        public async Task PreCacheDataAsync() => await EnsureDataLoaded();

        public async Task<IDepartment?> GetDepartmentAsync(string departmentNumber)
        {
            await EnsureDataLoaded();
            return _departments?.FirstOrDefault(d => d.Number == departmentNumber);
        }

        // Support Team Lookup
        public async Task<SupportTeam?> GetSupportTeamAsync(string teamName)
        {
            await EnsureDataLoaded();
            if (string.IsNullOrWhiteSpace(teamName)) return null;
            _teamMap.TryGetValue(teamName.Trim(), out var team);
            return team;
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

        // Department JSON Global used here
        private async Task LoadDepartmentsInternalAsync(bool isReload)
        {
            var key = isReload ? "DeptData.Reload" : "DeptData.Load";
            var progressKey = UiNotify.ProgressOf(key);
            UiNotify.Progress(key, isReload ? "Refreshing department data…" : "Loading department data…", priority: 0);

            // Resolve effective settings
            var settings = App.Settings?.Paths?.DepartmentData;

            // Default to Global
            string source = "web";
            string targetUri = Globals.g_DepartmentJSONURL;

            // Check if user has enabled override
            if (settings != null && settings.UseCustomSource && !string.IsNullOrWhiteSpace(settings.Uri))
            {
                source = settings.Source;
                targetUri = settings.Uri;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                DepartmentListWrapper? wrapper = null;

                // Switch on source type
                if (source.Equals("file", StringComparison.OrdinalIgnoreCase))
                {
                    // File Mode
                    Log.Info("Dept.Loader", $"Loading from local file: {targetUri}");

                    if (File.Exists(targetUri))
                    {
                        var json = await File.ReadAllTextAsync(targetUri);
                        wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(json);

                        // Intelligent Cache Update: Only update if local file is newer than current cache
                        if (wrapper != null)
                        {
                            var cached = await _fileCache.ReadAsync();
                            var fileStamp = wrapper.Meta?.LastUpdatedUtc;
                            var cacheStamp = cached?.Meta?.LastUpdatedUtc;

                            bool shouldCache =
                                cached == null ||
                                (fileStamp.HasValue && (!cacheStamp.HasValue || fileStamp > cacheStamp));

                            if (shouldCache)
                            {
                                try
                                {
                                    await _fileCache.WriteAsync(wrapper);
                                    Log.Info("Dept.Loader", "Local file was newer than cache. Cache updated.");
                                }
                                catch (Exception ex)
                                {
                                    Log.Warn("Dept.Loader", $"Failed to update cache from local file: {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException($"Configured data file not found: {targetUri}");
                    }
                }
                else
                {
                    // Web Mode (Standard)
                    wrapper = await RemoteWithBackUpLoader.LoadAsync(
                        _http,
                        targetUri,
                        _fileCache,
                        StampSelector,
                        ct: default,
                        jsonSettings: null,
                        normalize: w => w.Meta?.Normalize(),
                        log: msg => Log.Info("Dept.Loader", msg));
                }

                if (wrapper == null)
                    throw new InvalidOperationException($"No department data available from source '{source}'.");

                _meta = wrapper.Meta;

                // Populate class-level Dictionary
                _teamMap = wrapper.SupportTeams?
                    .Where(t => !string.IsNullOrWhiteSpace(t.SupportTeamName))
                    .ToDictionary(t => t.SupportTeamName.Trim(), StringComparer.OrdinalIgnoreCase)
                    ?? [];

                // LOGGING: Confirm team count
                Log.Info("Dept.Loader", $"Loaded {_teamMap.Count} support team definitions.");

                // Map Departments and Link Support Teams
                int linkedCount = 0;
                _departments = [.. (wrapper.DepartmentList ?? []).Select(d =>
                {
                    SupportTeam? matchedTeam = null;

                    // Check if department has a team assignment
                    if (!string.IsNullOrWhiteSpace(d.Team))
                    {
                        var teamName = d.Team.Trim();

                        // Try to find the team
                        if (_teamMap.TryGetValue(teamName, out var t))
                        {
                            matchedTeam = t;
                            linkedCount++;
                        }
                        else
                        {
                            // LOGGING: Warn about broken links
                            Log.Warn("Dept.Loader", $"Department '{d.Number}' references unknown team '{teamName}'. Check JSON spelling.");
                        }
                    }

                    return new DepartmentAdapter(d, matchedTeam);
                })];

                sw.Stop();

                // LOGGING: Summary of linking
                Log.Info("Dept.Loader", $"Mapped {linkedCount} departments to their support teams out of {_departments.Count} total.");

                UiNotify.RemoveKey(progressKey);
                UiNotify.Success($"{(isReload ? "Refreshed" : "Loaded")} {_departments.Count} departments from {source}.",
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

                // Ensure error is logged to disk as well
                Log.Error("Dept.Loader", "Fatal error loading department data", e);
            }
        }

        // UTC stamp used for backup refresh decisions.
        private static DateTime? StampSelector(DepartmentListWrapper w) => w.Meta?.LastUpdatedUtc;

        // Thin adapter to keep UI decoupled from transport DTOs.
        private sealed class DepartmentAdapter(Department source, SupportTeam? teamInfo) : IDepartment
        {
            private readonly Department _source = source;
            private readonly SupportTeam? _teamInfo = teamInfo;

            public string Number => _source.Number;
            public bool SupportKnown => _source.SupportKnown;
            public string? Team => string.IsNullOrWhiteSpace(_source.Team) ? null : _source.Team.Trim();
            public string? Notes => _source.Notes;
            public string? FileRepoPath => string.IsNullOrWhiteSpace(_source.FileRepoPath) ? null : _source.FileRepoPath;

            public string? ManagerName => _teamInfo?.ManagerName;
            public string? ManagerNetId => _teamInfo?.ManagerNetID;

            public string? SupportPhoneNumber =>
                string.IsNullOrWhiteSpace(_teamInfo?.PhoneNumber)
                    ? null
                    : _teamInfo.PhoneNumber.Trim();

            public List<SupportedDivs>? SupportedDivisions => _teamInfo?.SupportedDivisions;
        }
    }
}