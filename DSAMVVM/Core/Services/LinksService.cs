using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace DSAMVVM.Core.Services
{
    // Service for loading Links data using a Remote-First strategy with local offline fallback.
    public class LinksService(IHttpService http) : ILinksService
    {
        // Keep injected service to satisfy DI signature
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _cachePath = Globals.g_LinksCachePath;

        private LinksData? _cache;

        public LinksData? GetCachedLinksData() => _cache;

        public Task<LinksData?> LoadLinksDataAsync() => LoadInternalAsync(isReload: false, CancellationToken.None);
        public Task ReloadLinksDataAsync() => LoadInternalAsync(isReload: true, CancellationToken.None);

        // Core logic for fetching, caching, and parsing data.
        private async Task<LinksData?> LoadInternalAsync(bool isReload, CancellationToken ct)
        {
            // Return memory cache immediately if available and not forcing a reload.
            if (!isReload && _cache != null) return _cache;

            await _gate.WaitAsync(ct);
            try
            {
                // Double-check locking pattern.
                if (!isReload && _cache != null) return _cache;

                var key = isReload ? "LinksData.Reload" : "LinksData.Load";
                var progressKey = UiNotify.ProgressOf(key);
                UiNotify.Progress(key, isReload ? "Refreshing links…" : "Loading links…", priority: 0);

                // Resolve effective source, defaulting to global web URL unless a valid custom source is enabled.
                var settings = App.Settings?.Paths?.LinksData;
                string source = "Web";
                string targetUri = Globals.g_LinksJSON;

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
                        Log.Info("Links.Loader", $"Fetching web data from: {targetUri}");

                        // Web Strategy: Always fetch fresh content to avoid stale data.
                        using var client = new HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(5);
                        var webContent = await client.GetStringAsync(targetUri, ct);

                        // Check existing cache.
                        string cachedContent = string.Empty;
                        if (File.Exists(_cachePath))
                        {
                            cachedContent = await File.ReadAllTextAsync(_cachePath, ct);
                        }

                        // Write-on-Change: Only overwrite disk cache if content differs.
                        if (!string.Equals(webContent, cachedContent, StringComparison.Ordinal))
                        {
                            EnsureDirectory(_cachePath);
                            await File.WriteAllTextAsync(_cachePath, webContent, ct);
                            Log.Info("Links.Loader", "Remote data changed. Cache updated.");
                        }
                        else
                        {
                            Log.Info("Links.Loader", "Remote data identical to cache. Skipping disk write.");
                        }

                        jsonContent = webContent;
                    }
                    else if (string.Equals(source, "File", StringComparison.OrdinalIgnoreCase))
                    {
                        // File Strategy: Read directly. Do not backup to cache to prevent dev/test files from polluting production fallback.
                        Log.Info("Links.Loader", $"Loading local file: {targetUri}");

                        if (File.Exists(targetUri))
                        {
                            jsonContent = await File.ReadAllTextAsync(targetUri, ct);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Custom file not found: {targetUri}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    UiNotify.RemoveKey(progressKey);
                    UiNotify.Info("Links download canceled.", showStatusBar: false, key: key);
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warn("Links.Loader", $"Primary load failed ({source}): {ex.Message}");

                    // Fallback: If web request fails (offline/timeout), load last known good state from cache.
                    if (string.Equals(source, "Web", StringComparison.OrdinalIgnoreCase) && File.Exists(_cachePath))
                    {
                        Log.Info("Links.Loader", "Falling back to local cache.");
                        try
                        {
                            jsonContent = await File.ReadAllTextAsync(_cachePath, ct);
                            loadedFromCache = true;
                        }
                        catch (Exception cacheEx)
                        {
                            Log.Error("Links.Loader", $"Cache read failed: {cacheEx.Message}");
                        }
                    }

                    // If we still have no content (Fatal Error), notify UI and return null.
                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        sw.Stop();
                        UiNotify.RemoveKey(progressKey);

                        var retryAction = UiNotify.Link.Action("Retry", async () => await ReloadLinksDataAsync());
                        var openLink = Uri.TryCreate(targetUri, UriKind.Absolute, out var uriResult) &&
                                       (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
                                       ? UiNotify.Link.External("Open source", uriResult)
                                       : null;

                        UiNotify.WarnWithLinks(
                            $"Failed to {(isReload ? "refresh" : "load")} links: {ex.Message}",
                            sticky: true,
                            priority: 3,
                            key: key,
                            retryAction,
                            openLink,
                            UiNotify.Link.OpenLogs());

                        return _cache; // Return existing cache (if any) or null
                    }
                }

                // Parse the data
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        var model = JsonConvert.DeserializeObject<LinksData>(jsonContent);

                        // normalize meta if needed
                        model?.Meta?.Normalize();

                        _cache = model;
                    }
                    catch (Exception e)
                    {
                        Log.Error("Links.Loader", "Fatal error parsing links data", e);
                        // Don't throw here, let the UI show the success/fail state below or generic error
                    }
                }

                sw.Stop();
                UiNotify.RemoveKey(progressKey);

                if (_cache != null)
                {
                    string sourceMsg = loadedFromCache ? "Cache (Offline)" : source;
                    UiNotify.Success($"Loaded links from {sourceMsg}.", showStatusBar: true, key: key);
                }
                else
                {
                    // Parsing failed essentially
                    UiNotify.WarnWithLinks("Links data loaded but could not be parsed.", sticky: true, priority: 3, key: key);
                }

                return _cache;
            }
            finally
            {
                _gate.Release();
            }
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
    }
}