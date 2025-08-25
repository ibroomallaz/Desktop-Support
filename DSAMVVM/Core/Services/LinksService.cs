using System.Diagnostics;
using DSAMVVM.Core.IO;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.Core.Services
{
    // Remote-first with local JSON fallback + conditional backup refresh.
    public class LinksService : ILinksService
    {
        private readonly IHttpService _http;
        private readonly SemaphoreSlim _gate = new(1, 1);                 // single-flight load/reload
        private readonly JsonFileCache<LinksData> _fileCache =
            new(Globals.g_LinksCachePath);
        private readonly Func<LinksData, DateTime?> _stamp;

        private LinksData? _cache;
        private readonly string _remoteUrl = Globals.g_LinksJSON;

        public LinksService(IHttpService http, Func<LinksData, DateTime?>? stampSelector = null)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _stamp = stampSelector ?? DefaultStamp;                        // UTC used for refresh decision
        }

        public LinksData? GetCachedLinksData() => _cache;

        public Task<LinksData?> LoadLinksDataAsync() => LoadInternalAsync(isReload: false, CancellationToken.None);
        public Task ReloadLinksDataAsync() => LoadInternalAsync(isReload: true, CancellationToken.None);

        private static DateTime? DefaultStamp(LinksData l) => l.Meta?.LastUpdatedUtc;

        private async Task<LinksData?> LoadInternalAsync(bool isReload, CancellationToken ct)
        {
            if (!isReload && _cache != null) return _cache;

            await _gate.WaitAsync(ct);
            try
            {
                if (!isReload && _cache != null) return _cache;

                var key = isReload ? "LinksData.Reload" : "LinksData.Load";
                var progressKey = UiNotify.ProgressOf(key);
                UiNotify.Progress(key, isReload ? "Refreshing links…" : "Loading links…", priority: 0);

                var sw = Stopwatch.StartNew();
                try
                {
                    // Loader: web-first; write backup when web stamp is newer (or no local); fallback to local on web failure.
                    var model = await RemoteWithBackUpLoader.LoadAsync(
                        _http,
                        _remoteUrl,
                        _fileCache,
                        _stamp,
                        ct,
                        jsonSettings: null,
                        normalize: m => m.Meta?.Normalize(),
                        log: msg => Log.Info("Links.Loader", msg));

                    if (model == null)
                        throw new InvalidOperationException("No links data available from web or local cache.");

                    _cache = model;

                    sw.Stop();
                    UiNotify.RemoveKey(progressKey);
                    UiNotify.Success("Links loaded.", showStatusBar: true, key: key);
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
                    UiNotify.WarnWithLinks(
                        $"Failed to {(isReload ? "refresh" : "load")} links: {e.Message}",
                        sticky: true,
                        priority: 3,
                        key: key,
                        UiNotify.Link.Action("Retry", async () => await ReloadLinksDataAsync()),
                        UiNotify.Link.External("Open source", new Uri(_remoteUrl)),
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
