using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;
using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace DSAMVVM.Core.Services
{
    // Loads Department data using a Remote-First strategy with local offline fallback.
    public class DepartmentService(IHttpService http, IApplicationStateService appStateService) : IDepartmentService
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly IApplicationStateService _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
        private readonly SemaphoreSlim _lock = new(1, 1);                   // single-flight load/reload
        private readonly string _cachePath = Globals.g_DepartmentCachePath;

        private List<IDepartment>? _departments;
        private DepartmentMeta? _meta;

        // Class-level dictionary for fast Team lookups
        private Dictionary<string, SupportTeam> _teamMap = [];

        public async Task PreCacheDataAsync() => await EnsureDataLoaded();

        public async Task<IDepartment?> GetDepartmentAsync(string departmentNumber)
        {
            await EnsureDataLoaded();

            // Caches the requested department number in the application state
            if (!string.IsNullOrWhiteSpace(departmentNumber))
            {
                _appStateService.RecentDepartment = departmentNumber.Trim();
            }

            var dept = _departments?.FirstOrDefault(d => d.Number == departmentNumber);

            // Caches the resolved support team in the application state
            if (dept != null && !string.IsNullOrWhiteSpace(dept.Team))
            {
                _appStateService.RecentSupportTeam = dept.Team;
            }

            return dept;
        }

        public async Task<SupportTeam?> GetSupportTeamAsync(string teamName)
        {
            await EnsureDataLoaded();
            if (string.IsNullOrWhiteSpace(teamName)) return null;

            if (_teamMap.TryGetValue(teamName.Trim(), out var team))
            {
                // Caches the resolved support team in the application state
                _appStateService.RecentSupportTeam = team.SupportTeamName ?? teamName.Trim();
            }

            return team;
        }

        public async Task<string?> GetTeamAsync(string departmentNumber) => (await GetDepartmentAsync(departmentNumber))?.Team;
        public async Task<string?> GetNotesAsync(string departmentNumber) => (await GetDepartmentAsync(departmentNumber))?.Notes;
        public async Task<bool?> IsSupportKnownAsync(string departmentNumber) => (await GetDepartmentAsync(departmentNumber))?.SupportKnown;
        public async Task<string?> GetFileRepoPathAsync(string departmentNumber) => (await GetDepartmentAsync(departmentNumber))?.FileRepoPath;

        public async Task<IEnumerable<SupportTeam>> GetTeamsByDivisionAsync(string divAbbrev)
        {
            await EnsureDataLoaded();
            if (string.IsNullOrWhiteSpace(divAbbrev)) return [];

            var search = divAbbrev.Trim();
            return _teamMap.Values
                .Where(t => t.SupportedDivisions?
                    .Any(d => string.Equals(d.DivAbbrev, search, StringComparison.OrdinalIgnoreCase)) == true);
        }

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

        // Core logic for fetching, caching, and parsing data.
        private async Task LoadDepartmentsInternalAsync(bool isReload)
        {
            var key = isReload ? "DeptData.Reload" : "DeptData.Load";
            var progressKey = UiNotify.ProgressOf(key);
            UiNotify.Progress(key, isReload ? "Refreshing department data…" : "Loading department data…", priority: 0);

            // Resolve source, defaulting to global web URL unless a valid custom source is enabled.
            var settings = App.Settings?.Paths?.DepartmentData;
            string source = "Web";
            string targetUri = Globals.g_DepartmentJSONURL;

            if (settings != null && settings.UseCustomSource && !string.IsNullOrWhiteSpace(settings.Uri))
            {
                source = settings.Source;
                targetUri = settings.Uri;
            }

            var sw = Stopwatch.StartNew();
            string jsonContent = string.Empty;
            bool loadedFromCache = false;

            try
            {
                if (string.Equals(source, "Web", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info("Dept.Loader", $"Fetching web data from: {targetUri}");

                    // Web Strategy: Always fetch fresh content to avoid stale data.
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var webContent = await client.GetStringAsync(targetUri);

                    // Check existing cache.
                    string cachedContent = string.Empty;
                    if (File.Exists(_cachePath))
                    {
                        cachedContent = await File.ReadAllTextAsync(_cachePath);
                    }

                    // Write-on-Change: Only overwrite disk cache if content differs.
                    if (!string.Equals(webContent, cachedContent, StringComparison.Ordinal))
                    {
                        EnsureDirectory(_cachePath);
                        await File.WriteAllTextAsync(_cachePath, webContent);
                        Log.Info("Dept.Loader", "Remote data changed. Cache updated.");
                    }
                    else
                    {
                        Log.Info("Dept.Loader", "Remote data identical to cache. Skipping disk write.");
                    }

                    jsonContent = webContent;
                }
                else if (string.Equals(source, "File", StringComparison.OrdinalIgnoreCase))
                {
                    // File Strategy: Read directly. Do not backup to cache to prevent dev/test files from polluting production fallback.
                    Log.Info("Dept.Loader", $"Loading local file: {targetUri}");

                    if (File.Exists(targetUri))
                    {
                        jsonContent = await File.ReadAllTextAsync(targetUri);
                    }
                    else
                    {
                        throw new FileNotFoundException($"Custom file not found: {targetUri}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Dept.Loader", $"Primary load failed ({source}): {ex.Message}");

                // Fallback: If web request fails (offline/timeout), load last known good state from cache.
                if (string.Equals(source, "Web", StringComparison.OrdinalIgnoreCase) && File.Exists(_cachePath))
                {
                    Log.Info("Dept.Loader", "Falling back to local cache.");
                    try
                    {
                        jsonContent = await File.ReadAllTextAsync(_cachePath);
                        loadedFromCache = true;
                    }
                    catch (Exception cacheEx)
                    {
                        Log.Error("Dept.Loader", $"Cache read failed: {cacheEx.Message}");
                    }
                }
                else
                {
                    // Irrecoverable error (File mode missing or no offline cache).
                    throw;
                }
            }

            // Parse and map data
            if (!string.IsNullOrEmpty(jsonContent))
            {
                try
                {
                    var wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(jsonContent) ?? throw new Exception("Deserialized data was null.");
                    _meta = wrapper.Meta;

                    // Build fast lookup dictionary for Support Teams.
                    _teamMap = wrapper.SupportTeams?
                        .Where(t => !string.IsNullOrWhiteSpace(t.SupportTeamName))
                        .ToDictionary(t => t.SupportTeamName.Trim(), StringComparer.OrdinalIgnoreCase)
                        ?? [];

                    Log.Info("Dept.Loader", $"Loaded {_teamMap.Count} support team definitions.");

                    // Hydrate Departments and link to Support Teams.
                    _departments = [.. (wrapper.DepartmentList ?? []).Select(d =>
                    {
                        SupportTeam? matchedTeam = null;
                        if (!string.IsNullOrWhiteSpace(d.Team))
                        {
                            if (_teamMap.TryGetValue(d.Team.Trim(), out var t)) matchedTeam = t;
                        }
                        return new DepartmentAdapter(d, matchedTeam);
                    })];

                    sw.Stop();
                    UiNotify.RemoveKey(progressKey);

                    string statusMsg = isReload ? "Refreshed" : "Loaded";
                    string sourceMsg = loadedFromCache ? "Cache (Offline)" : source;

                    UiNotify.Success($"{statusMsg} {_departments.Count} departments from {sourceMsg}.",
                                     showStatusBar: true, key: key);
                }
                catch (Exception e)
                {
                    sw.Stop();
                    UiNotify.RemoveKey(progressKey);
                    Log.Error("Dept.Loader", "Fatal error parsing department data", e);

                    UiNotify.WarnWithLinks(
                        $"Failed to parse department data: {e.Message}",
                        sticky: true, priority: 3, key: key,
                        UiNotify.Link.Action("Retry", async () => await ReloadDataAsync(), "Try again"));
                }
            }
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

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
                string.IsNullOrWhiteSpace(_teamInfo?.PhoneNumber) ? null : _teamInfo.PhoneNumber.Trim();

            public List<SupportedDivs>? SupportedDivisions => _teamInfo?.SupportedDivisions;
        }
    }
}