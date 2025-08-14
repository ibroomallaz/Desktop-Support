using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
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
        private const string StatusKey = "LinksService";

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

        public Task<LinksData?> LoadLinksDataAsync() => LoadInternalAsync(force: false, CancellationToken.None);

        public Task ReloadLinksDataAsync() => LoadInternalAsync(force: true, CancellationToken.None);

        private async Task<LinksData?> LoadInternalAsync(bool force, CancellationToken ct)
        {
            if (!force && _cache != null) return _cache;

            await _gate.WaitAsync(ct);
            try
            {
                if (!force && _cache != null) return _cache;

                UiNotify.Info("Downloading links…", showStatusBar: true, key: StatusKey);

                string json = await _http.GetStringAsync(Globals.g_LinksJSON, ct);
                var data = JsonConvert.DeserializeObject<LinksData>(json, JsonSettings);

                if (data == null)
                {
                    UiNotify.WarnWithLinks(
                        "Links could not be parsed.",
                        sticky: true,
                        priority: 3,
                        key: StatusKey,
                        UiNotify.Link.Action("Retry", () => ReloadLinksDataAsync()),
                        UiNotify.Link.External("Open source", new Uri(Globals.g_LinksJSON)),
                        UiNotify.Link.OpenLogs()
                    );
                    return _cache; // keep prior cache if any
                }

                _cache = data;
                UiNotify.Success("Links loaded successfully.", showStatusBar: true, key: StatusKey);
                return _cache;
            }
            catch (OperationCanceledException)
            {
                UiNotify.Info("Links download canceled.", showStatusBar: false, key: StatusKey);
                throw;
            }
            catch (HttpRequestException ex)
            {
                UiNotify.WarnWithLinks(
                    $"Network error while retrieving links: {ex.Message}",
                    sticky: true,
                    priority: 3,
                    key: StatusKey,
                    UiNotify.Link.Action("Retry", () => ReloadLinksDataAsync()),
                    UiNotify.Link.External("Open source", new Uri(Globals.g_LinksJSON)),
                    UiNotify.Link.OpenLogs()
                );
                return _cache;
            }
            catch (JsonException ex)
            {
                UiNotify.WarnWithLinks(
                    $"Invalid links JSON: {ex.Message}",
                    sticky: true,
                    priority: 3,
                    key: StatusKey,
                    UiNotify.Link.Action("Retry", () => ReloadLinksDataAsync()),
                    UiNotify.Link.External("Open source", new Uri(Globals.g_LinksJSON)),
                    UiNotify.Link.OpenLogs()
                );
                return _cache;
            }
            catch (Exception ex)
            {
                UiNotify.WarnWithLinks(
                    $"Error loading links: {ex.Message}",
                    sticky: true,
                    priority: 3,
                    key: StatusKey,
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
