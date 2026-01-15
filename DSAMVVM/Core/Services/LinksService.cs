using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.IO;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;

namespace DSAMVVM.Core.Services
{
    // Remote-first with local JSON fallback + conditional backup refresh.
    public class LinksService(IHttpService http, Func<LinksData, DateTime?>? stampSelector = null) : ILinksService
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly SemaphoreSlim _gate = new(1, 1);                 // single-flight load/reload
        private readonly JsonFileCache<LinksData> _fileCache =
            new(Globals.g_LinksCachePath);
        private readonly Func<LinksData, DateTime?> _stamp = stampSelector ?? DefaultStamp;

        private LinksData? _cache;

        // This is now dynamic based on settings, so we don't store it as a readonly field
        // private readonly string _remoteUrl = Globals.g_LinksJSON; 

        public LinksData? GetCachedLinksData() => _cache;

        public Task<LinksData?> LoadLinksDataAsync() => LoadInternalAsync(isReload: false, CancellationToken.None);
        public Task ReloadLinksDataAsync() => LoadInternalAsync(isReload: true, CancellationToken.None);

        private static DateTime? DefaultStamp(LinksData l) => l.Meta?.LastUpdatedUtc;

        private async Task<LinksData?> LoadInternalAsync(bool isReload, CancellationToken ct)
        {
            // Return memory cache if available and not forcing a reload
            if (!isReload && _cache != null) return _cache;

            await _gate.WaitAsync(ct);
            try
            {
                if (!isReload && _cache != null) return _cache;

                var key = isReload ? "LinksData.Reload" : "LinksData.Load";
                var progressKey = UiNotify.ProgressOf(key);
                UiNotify.Progress(key, isReload ? "Refreshing links…" : "Loading links…", priority: 0);

                // Resolve effective settings for data source
                var settings = App.Settings?.Paths?.LinksData;

                // Default to standard global URL
                string source = "web";
                string targetUri = Globals.g_LinksJSON;

                // Check if user has enabled override
                if (settings != null && settings.UseCustomSource && !string.IsNullOrWhiteSpace(settings.Uri))
                {
                    source = settings.Source;
                    targetUri = settings.Uri;
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    LinksData? model = null;

                    // Switch logic based on source type
                    if (source.Equals("file", StringComparison.OrdinalIgnoreCase))
                    {
                        // File Mode handling
                        Log.Info("Links.Loader", $"Loading from local file: {targetUri}");

                        if (File.Exists(targetUri))
                        {
                            var json = await File.ReadAllTextAsync(targetUri, ct);
                            model = JsonConvert.DeserializeObject<LinksData>(json);

                            // Intelligent Cache Update: Write to cache if local file is newer than current cache
                            if (model != null)
                            {
                                var cached = await _fileCache.ReadAsync();
                                var fileStamp = _stamp(model);
                                var cacheStamp = cached != null ? _stamp(cached) : null;

                                bool shouldCache =
                                    cached == null ||
                                    (fileStamp.HasValue && (!cacheStamp.HasValue || fileStamp > cacheStamp));

                                if (shouldCache)
                                {
                                    try
                                    {
                                        await _fileCache.WriteAsync(model);
                                        Log.Info("Links.Loader", "Local file was newer than cache. Cache updated.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warn("Links.Loader", $"Failed to update cache from local file: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new FileNotFoundException($"Configured links file not found: {targetUri}");
                        }
                    }
                    else
                    {
                        // Web Mode (Standard) uses the resilient loader
                        model = await RemoteWithBackUpLoader.LoadAsync(
                            _http,
                            targetUri, // Uses the resolved URI (Default or Custom)
                            _fileCache,
                            _stamp,
                            ct,
                            jsonSettings: null,
                            normalize: m => m.Meta?.Normalize(),
                            log: msg => Log.Info("Links.Loader", msg)) ?? throw new InvalidOperationException("No links data available from web or local cache.");
                    }

                    _cache = model;

                    sw.Stop();
                    UiNotify.RemoveKey(progressKey);
                    UiNotify.Success($"Loaded links from {source}.", showStatusBar: true, key: key);
                    return _cache;
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    UiNotify.RemoveKey(progressKey);
                    UiNotify.Info("Links download canceled.", showStatusBar: false, key: key);
                    throw;
                }
                catch (Exception e)
                {
                    sw.Stop();
                    UiNotify.RemoveKey(progressKey);

                    // Build retry actions for the UI notification
                    var retryAction = UiNotify.Link.Action("Retry", async () => await ReloadLinksDataAsync());

                    // Only show "Open source" link if it is a valid web URL
                    var openLink = Uri.TryCreate(targetUri, UriKind.Absolute, out var uriResult) &&
                                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
                                   ? UiNotify.Link.External("Open source", uriResult)
                                   : null;

                    UiNotify.WarnWithLinks(
                        $"Failed to {(isReload ? "refresh" : "load")} links: {e.Message}",
                        sticky: true,
                        priority: 3,
                        key: key,
                        retryAction,
                        openLink,
                        UiNotify.Link.OpenLogs());

                    return _cache;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}