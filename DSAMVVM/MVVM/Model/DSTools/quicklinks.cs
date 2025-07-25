using DSAMVVM.Core;
using DSAMVVM.MVVM.Model.utils;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace DSAMVVM.MVVM.Model.DSTools
{
    public class QuickLinks
    {
        // Cached deserialized links for reuse
        private static QuickLinksData? cachedLinks = null;
        // Shared static HttpClient for reuse
        private static readonly HttpClient client = new();

        private readonly IStatusReporter _status;


        public QuickLinks(IStatusReporter status)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _ = GetQuickLinksDataAsync(); // Fire-and-forget on load
        }

        public async Task<QuickLinksData?> GetQuickLinksDataAsync()
        {
            if (cachedLinks == null)
            {
                try
                {
                    _status.Report(StatusMessageFactory.Plain("Downloading Links Data..."));

                    string json = await client.GetStringAsync(Globals.g_LinksJSON);
                    cachedLinks = JsonConvert.DeserializeObject<QuickLinksData>(json);

                    if (cachedLinks == null)
                    {
                        // JSON failed to deserialize — show retry
                        _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                            $"Deserialization returned null. {{0}}",
                            new Inline[] { StatusMessageFactory.ActionLink("Retry", () => _ = ReloadQuickLinksDataAsync()) },
                            priority: 3, sticky: true, key: "Quicklinks"));
                    }
                    else
                    {
                        _status.Report(StatusMessageFactory.Plain("Quick Links loaded successfully.", priority: 0, sticky: false));
                    }
                }
                catch (Exception ex)
                {
                    // Network or deserialization error
                    _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                        $"Error retrieving or deserializing JSON: {ex.Message}. {{0}}",
                        new Inline[] { StatusMessageFactory.ActionLink("Retry", () => _ = ReloadQuickLinksDataAsync()) },
                        priority: 3, sticky: true, key: "Quicklinks"));
                }
            }

            return cachedLinks;
        }

        public async Task ReloadQuickLinksDataAsync()
        {
            try
            {
                string json = await client.GetStringAsync(Globals.g_LinksJSON);
                cachedLinks = JsonConvert.DeserializeObject<QuickLinksData>(json);

                if (cachedLinks != null)
                {
                    _status.Report(StatusMessageFactory.Plain("Quick Links reloaded successfully.", priority: 0, sticky: false, key: "Quicklinks"));
                }
            }
            catch (Exception ex)
            {
                _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                    $"Error reloading quick links: {ex.Message}. {{0}}",
                    new Inline[] { StatusMessageFactory.ActionLink("Retry", () => _ = ReloadQuickLinksDataAsync()) },
                    priority: 3, sticky: true, key: "Quicklinks"));
            }
        }

        // Root container for parsed QuickLinks data from JSON.
        public class QuickLinksData
        {
            public List<Link> CommonLinks { get; set; } = new(); // Shared links
            public List<TeamLinkGroup> TeamLinks { get; set; } = new(); // Per-team links
        }

        // Represents a team-specific group of links.
        public class TeamLinkGroup
        {
            public string Team { get; set; } = "";
            public List<Link> Links { get; set; } = new();
        }

        // Link entries.

        public class Link
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string URL { get; set; } = "";
        }
    }
}
