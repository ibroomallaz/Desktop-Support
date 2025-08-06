using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    public class LinksService(IStatusReporter status) : ILinksService
    {
        private static LinksData? _cachedLinks;
        private static readonly HttpClient _client = new();
        private readonly IStatusReporter _status = status ?? throw new ArgumentNullException(nameof(status));

        public async Task<LinksData?> LoadLinksDataAsync()
        {
            if (_cachedLinks == null)
            {
                try
                {
                    _status.Report(StatusMessageFactory.Plain("Downloading Links Data..."));
                    string json = await _client.GetStringAsync(Globals.g_LinksJSON);
                    _cachedLinks = JsonConvert.DeserializeObject<LinksData>(json);

                    if (_cachedLinks == null)
                    {
                        _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                            "Deserialization returned null. {0}",
                            [StatusMessageFactory.ActionLink("Retry", () => _ = ReloadLinksDataAsync())],
                            priority: 3, sticky: true, key: "LinksService"));
                    }
                    else
                    {
                        _status.Report(StatusMessageFactory.Plain("Links loaded successfully.", priority: 0));
                    }
                }
                catch (Exception ex)
                {
                    _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                        $"Error retrieving or deserializing links: {ex.Message}. {{0}}",
                        [StatusMessageFactory.ActionLink("Retry", () => _ = ReloadLinksDataAsync())],
                        priority: 3, sticky: true, key: "LinksService"));
                }
            }

            return _cachedLinks;
        }

        public async Task ReloadLinksDataAsync()
        {
            try
            {
                string json = await _client.GetStringAsync(Globals.g_LinksJSON);
                _cachedLinks = JsonConvert.DeserializeObject<LinksData>(json);

                if (_cachedLinks != null)
                {
                    _status.Report(StatusMessageFactory.Plain("Links reloaded successfully.", priority: 0, sticky: false, key: "LinksService"));
                }
            }
            catch (Exception ex)
            {
                _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                    $"Error reloading links: {ex.Message}. {{0}}",
                    [StatusMessageFactory.ActionLink("Retry", () => _ = ReloadLinksDataAsync())],
                    priority: 3, sticky: true, key: "LinksService"));
            }
        }
        public LinksData? GetCachedLinksData()
        {
            return _cachedLinks;
        }

    }
}
