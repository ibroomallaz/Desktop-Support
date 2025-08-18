using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    public class LinksService : ILinksService
    {
        private readonly IHttpService _http;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private LinksData? _cache;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };

        public LinksService(IHttpService http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public LinksData? GetCachedLinksData() => _cache;

        public Task<LinksData?> LoadLinksDataAsync() => LoadInternalAsync(isReload: false, CancellationToken.None);
        public Task ReloadLinksDataAsync() => LoadInternalAsync(isReload: true, CancellationToken.None);

        private async Task<LinksData?> LoadInternalAsync(bool isReload, CancellationToken ct)
        {
            if (!isReload && _cache != null) return _cache;

            await _gate.WaitAsync(ct);
            try
            {
                if (!isReload && _cache != null) return _cache;

                string baseKey = isReload ? "LinksData.Reload" : "LinksData.Load";
                string progressKey = UiNotify.ProgressOf(baseKey);

                UiNotify.Progress(baseKey, isReload ? "Refreshing links…" : "Downloading links…");

                string json = await _http.GetStringAsync(Globals.g_LinksJSON, ct);
                var data = JsonConvert.DeserializeObject<LinksData>(json, JsonSettings);

                if (data == null)
                {
                    UiNotify.RemoveKey(progressKey);
                    UiNotify.WarnWithLinks(
                        "Links could not be parsed.",
                        sticky: true, priority: 3, key: baseKey,
                        UiNotify.Link.Action("Retry", () => ReloadLinksDataAsync()),
                        UiNotify.Link.External("Open source", new Uri(Globals.g_LinksJSON)),
                        UiNotify.Link.OpenLogs()
                    );
                    return _cache;
                }

                _cache = data;

                UiNotify.RemoveKey(progressKey);
                UiNotify.Success("Links loaded successfully.", showStatusBar: true, key: baseKey);
                return _cache;
            }
            catch (OperationCanceledException)
            {
                string baseKey = isReload ? "LinksData.Reload" : "LinksData.Load";
                UiNotify.RemoveKey(UiNotify.ProgressOf(baseKey));
                UiNotify.Info("Links download canceled.", showStatusBar: false, key: baseKey);
                throw;
            }
            catch (HttpRequestException ex)
            {
                string baseKey = isReload ? "LinksData.Reload" : "LinksData.Load";
                UiNotify.RemoveKey(UiNotify.ProgressOf(baseKey));
                UiNotify.WarnWithLinks(
                    $"Network error while retrieving links: {ex.Message}",
                    sticky: true, priority: 3, key: baseKey,
                    UiNotify.Link.Action("Retry", () => ReloadLinksDataAsync()),
                    UiNotify.Link.External("Open source", new Uri(Globals.g_LinksJSON)),
                    UiNotify.Link.OpenLogs()
                );
                return _cache;
            }
            catch (JsonException ex)
            {
                string baseKey = isReload ? "LinksData.Reload" : "LinksData.Load";
                UiNotify.RemoveKey(UiNotify.ProgressOf(baseKey));
                UiNotify.WarnWithLinks(
                    $"Invalid links JSON: {ex.Message}",
                    sticky: true, priority: 3, key: baseKey,
                    UiNotify.Link.Action("Retry", () => ReloadLinksDataAsync()),
                    UiNotify.Link.External("Open source", new Uri(Globals.g_LinksJSON)),
                    UiNotify.Link.OpenLogs()
                );
                return _cache;
            }
            catch (Exception ex)
            {
                string baseKey = isReload ? "LinksData.Reload" : "LinksData.Load";
                UiNotify.RemoveKey(UiNotify.ProgressOf(baseKey));
                UiNotify.WarnWithLinks(
                    $"Error loading links: {ex.Message}",
                    sticky: true, priority: 3, key: baseKey,
                    UiNotify.Link.Action("Retry", () => ReloadLinksDataAsync()),
                    UiNotify.Link.External("Open source", new Uri(Globals.g_LinksJSON)),
                    UiNotify.Link.OpenLogs()
                );
                return _cache;
            }
            finally
            {
                _gate.Release();
            }
        }

    }
}
