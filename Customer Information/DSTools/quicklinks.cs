using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;

public class QuickLinks
{
    // In-memory cache for the deserialized quick links
    private static Links cachedLinks = null;
    private static readonly HttpClient client = new HttpClient();

    public static async Task<Links> GetQuickLinksDataAsync()
    {
        if (cachedLinks == null)
        {
            try
            {
                Console.WriteLine("Downloading Quicklinks data...");
                string json = await client.GetStringAsync(Globals.g_QuickLinksURL);
                cachedLinks = JsonConvert.DeserializeObject<Links>(json);
                if (cachedLinks == null || cachedLinks.QL == null)
                {
                    Console.WriteLine("Deserialization resulted in null or invalid data.");
                    return null;
                }
                Console.Clear();
                return cachedLinks;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving or deserializing JSON: " + ex);
                return null;
            }
        }
        return cachedLinks;
    }

    public static async Task ReloadQuickLinksDataAsync()
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {

                string json = await client.GetStringAsync(Globals.g_QuickLinksURL);
                cachedLinks = JsonConvert.DeserializeObject<Links>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reloading quick links: " + ex);
            }
        }
    }

    public class Links
    {
        public Link[] QL { get; set; }

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
        public string Name { get; set; }
        public string Number { get; set; }
        public string Description { get; set; }
        public string URL { get; set; }
    }

    public static async Task PrintQL(Links quicklinks)
    {
        if (quicklinks?.QL == null || quicklinks.QL.Length == 0)
        {
            Console.WriteLine("No links found.");
            return;
        }

        foreach (var link in quicklinks.QL)
        {
            ColoredConsole.WriteLine($"({link.Number.Red()}) {link.Name.Cyan()}: {link.Description}");
        }
    }


    public static async Task QLMainMenu()
    {
        
        Console.Clear();
        Links quicklinks = await GetQuickLinksDataAsync();
        bool quickLinksMenu = true;

        while (quickLinksMenu)
        {
            Console.WriteLine("Desktop Support Quick Links:\n");
            ColoredConsole.WriteLine($"Open the desired link by typing in the corresponding number and hitting enter.");
            ColoredConsole.WriteLine($"At any time: type '{DarkYellow("back")}' to move back one menu, '{Cyan("clear")}' to clear the text, '{Red("exit")}' to go back to main menu\n");
            await PrintQL(quicklinks);
            Console.WriteLine();

            string quickLinksAnswer = Console.ReadLine().ToLower().Trim();

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
                        Console.Clear();
                        quicklinks.OpenURL(num);
                        Console.WriteLine();
                    }
                    break;
            }
        }
    }
}
