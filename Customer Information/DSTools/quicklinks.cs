using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;

//Class to allow for link changes on the fly via json without needing a new compiled exe
//json will be delivered via box and is deserialized into Links Class for ease of use
public class QuickLinks
{
    //stored location of the json housing the quick link data
    const string JsonURL = "https://arizona.box.com/shared/static/4jonapcgzw5lq2i8m40doma5x9t684de.json";
    public static async Task GetJson()
    {
        //check for %localappdata%\Desktop_Support_App and create folder if it doesn't exist
        string path = Environment.GetEnvironmentVariable("LocalAppData") + @"\Desktop_Support_App\";
        try
        {
            if (!Directory.Exists(path))
            {
                DirectoryInfo dir = Directory.CreateDirectory(path);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"The process failed: {0}", e.ToString());
        }
        string fileName = path + @"\quicklink.json";

        await HTTP.DownloadFile(JsonURL, fileName);
    }

    public static Links ReadJson()
    {

        string filePath = Environment.GetEnvironmentVariable("LocalAppData") + @"\Desktop_Support_App\quicklink.json";
        if (!File.Exists(filePath))
        {
            GetJson();
            Console.WriteLine("Quicklink data missing. Attempting to redownload Data");
        }
        using StreamReader reader = new StreamReader(filePath);
        string json = reader.ReadToEnd();

        try
        {
            return JsonConvert.DeserializeObject<Links>(json);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            return null;
        }
    }
    public class Links
    {
        public Link[] QL { get; set; }

        public void OpenURL(int num)
        {
            int inum = (num -1);
            if (num <= QL.Length)
            {
                string url = QL[(inum)].URL;
                HTTP.OpenURL(url);
                ColoredConsole.WriteLine($"{Green("Opening")} {QL[(num - 1)].Name}");
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
public static void PrintQL(Links quicklinks)
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

    public static void QLMain()
    {
        Console.Clear();
        Links quicklinks = ReadJson();
        bool quickLinksMenu = true;
        while (quickLinksMenu)
        {
        Console.WriteLine("Desktop Support Quick Links:\n");
        ColoredConsole.WriteLine($"Open the desired like by typing in the coresponding number and hitting enter.");
        ColoredConsole.WriteLine($"At any time: type '{DarkYellow("back")}' to move back one menu, '{Cyan("clear")}' to clear the text, '{Red("exit")}' to go back to main menu\n");
        PrintQL(quicklinks);
            Console.WriteLine();
            string quickLinksAnswer = Console.ReadLine().ToLower().Trim();
            switch (quickLinksAnswer)
            {

                default:
                    int num;
                    if (int.TryParse(quickLinksAnswer, out num))
                    {
                        Console.Clear();
                        quicklinks.OpenURL(num);
                        Console.WriteLine();
                    }
                    break;
                case "back":
                    quickLinksMenu = false;
                    DSTools.DSToolsMenu();
                    break;
                case "exit":
                    quickLinksMenu = false;
                    Console.Clear();
                    Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "-reload":
                    GetJson();
                    quicklinks = ReadJson();
                    break;
            }
        }
    }
    
}
