using Colors.Net;
using Colors.Net.StringColorExtensions;
using DSAMVVM.Core;
using DSAMVVM.MVVM.Model.utils;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using static Colors.Net.StringStaticMethods;

namespace DSAMVVM.MVVM.Model.DSTools
{
    public class QuickLinks
    {
        // In-memory cache for the deserialized quick links
        private static Links? cachedLinks = null;
        private static readonly HttpClient client = new HttpClient();
        private readonly IStatusReporter _status;
        public QuickLinks(IStatusReporter status, HttpClient? client = null)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
            client = client ?? new HttpClient();
            _ = GetQuickLinksDataAsync();
        }

        public async Task<Links?> GetQuickLinksDataAsync()
        {
            if (cachedLinks == null)
            {
                try
                {

                    _status.Report(StatusMessageFactory.Plain("Downloading Links Data..."));
                    string json = await client.GetStringAsync(Globals.g_QuickLinksURL);
                    cachedLinks = JsonConvert.DeserializeObject<Links>(json);
                    if (cachedLinks == null || cachedLinks.QL == null)
                    {
                        _status.Report(StatusMessageFactory.CreateRichInternalMessage($"Deserialization resulted in null or invalid data.: {{0}}",
                            new Inline[]
                            {
                                 StatusMessageFactory.ActionLink("Retry", () => _ = ReloadQuickLinksDataAsync())
                            },
                            priority: 3,
                            sticky: true,
                            key: "Quicklinks"));
                    }
                    _status.Report(StatusMessageFactory.Plain("Quick Links loaded successfully.", priority: 0, sticky: false));
                    return cachedLinks;
                }
                catch (Exception ex)
                {
                    _status.Report(StatusMessageFactory.CreateRichInternalMessage($"Error retrieving or deserializing JSON: {ex.Message}. {{0}}",
                        new Inline[]
                        {
                             StatusMessageFactory.ActionLink("Retry", () => _ = ReloadQuickLinksDataAsync())
                        },
                        priority: 3,
                        sticky: true,
                        key: "Quicklinks"));
                }
            }
            return cachedLinks;
        }

        public async Task ReloadQuickLinksDataAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string json = await client.GetStringAsync(Globals.g_QuickLinksURL);
                    cachedLinks = JsonConvert.DeserializeObject<Links>(json);

                    if (cachedLinks != null && cachedLinks.QL != null)
                    {
                        _status.Report(StatusMessageFactory.Plain(
                            "Quick Links reloaded successfully.",
                            priority: 0,
                            sticky: false,
                            key: "Quicklinks"));
                    }
                }
                catch (Exception ex)
                {
                    _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                        $"Error reloading quick links: {ex.Message}: {{0}}",
                        new Inline[]
                        {
                    StatusMessageFactory.ActionLink("Retry", () => _ = ReloadQuickLinksDataAsync())
                        },
                        priority: 3,
                        sticky: true,
                        key: "Quicklinks"));
                }
            }
        }


        public class Links
        {
            public Link[] QL { get; set; } = Array.Empty<Link>();

            public void OpenURL(int num)
            {
                int index = num - 1;
                if (index < QL.Length)
                {
                    string url = QL[index].URL;
                    HTTP.OpenURL(url);
                    ColoredConsole.WriteLine($"{Green("Opening")} {QL[index].Name}");
                }
                else
                {
                    Console.WriteLine($"Option {num} does not exist.");
                }
            }
        }

        public class Link
        {
            public string Name { get; set; } = string.Empty;
            public string Number { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string URL { get; set; } = string.Empty;
        }

        //Old print method
        public static Task PrintQL(Links? quicklinks)
        {
            if (quicklinks?.QL == null || quicklinks.QL.Length == 0)
            {
                Console.WriteLine("No links found. Please Reload");
                return Task.CompletedTask;
            }

            foreach (var link in quicklinks.QL)
            {
                ColoredConsole.WriteLine($"({link.Number.Red()}) {link.Name.Cyan()}: {link.Description}");
            }
            return Task.CompletedTask;
        }

        //Old menu.. Kept for methods until no longer needed
        public async Task QLMainMenu()
        {

            Console.Clear();
            Links? quicklinks = await GetQuickLinksDataAsync();
            bool quickLinksMenu = true;

            while (quickLinksMenu)
            {
                Console.WriteLine("Desktop Support Quick Links:\n");
                ColoredConsole.WriteLine($"Open the desired link by typing in the corresponding number and hitting enter.");
                ColoredConsole.WriteLine($"At any time: type '{DarkYellow("back")}' to move back one menu, '{Cyan("clear")}' to clear the text, '{Red("exit")}' to go back to main menu\n");
                await PrintQL(quicklinks);
                Console.WriteLine();

                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                string quickLinksAnswer = input.ToLower().Trim();

                switch (quickLinksAnswer)
                {
                    case "back":
                        quickLinksMenu = false;
                        await DSTools.DSToolsMenu();
                        break;
                    case "exit":
                        quickLinksMenu = false;
                        Console.Clear();
                        await Menus.MainMenu();
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                    case "-reload":
                        await ReloadQuickLinksDataAsync();
                        quicklinks = cachedLinks;
                        break;
                    default:
                        if (int.TryParse(quickLinksAnswer, out int num))
                        {
                            if (quicklinks != null)
                            {
                                Console.Clear();
                                quicklinks.OpenURL(num);
                                Console.WriteLine();
                            }
                            else
                            {
                                Console.WriteLine("Quicklinks data is unavailable. Please Reload");
                            }

                        }
                        break;
                }
            }
        }
    }
}
