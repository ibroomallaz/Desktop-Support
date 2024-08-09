using System.Net;
using Colors.Net;
using static Colors.Net.StringStaticMethods;
using Colors.Net.StringColorExtensions;
class CSV
{
    public static List<Dictionary<string, string>> _entries;

    //Download CSV from box
    public static async Task GetCSV()
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
            Console.WriteLine("The process failed: {0}", e.ToString());
        }
        var url = new Uri("https://arizona.box.com/shared/static/27qy9jc64b0cpz4l6zzeu8pnri65y4d0.csv");
        string fileName = path + @"\ci.csv";
        await HTTP.DownloadFile(url, fileName);
        //Obsolete method using webClient, but simple code
    }
    public static async Task CSVMain()
    {
        string filePath = Environment.GetEnvironmentVariable("LocalAppData") + @"\Desktop_Support_App\ci.csv";
        
        _entries = ReadCSV(filePath);
    }
    // Read CSV and store entries
    static List<Dictionary<string, string>> ReadCSV(string filePath)
    {
        List<Dictionary<string, string>> entries = new List<Dictionary<string, string>>();

        // Read CSV file
        using (var reader = new StreamReader(filePath))
        {
            // Read headers
            var headers = reader.ReadLine()?.Split(',');

            // Read data
            while (!reader.EndOfStream)
            {
                var values = reader.ReadLine()?.Split(',');
                if (values != null)
                {
                    var entry = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Length; i++)
                    {
                        if (i < values.Length)
                            entry.Add(headers[i], values[i]);
                    }
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }
    //Main printing function from CSV data
    public static void PrintDepartmentInfo(string department)
    {
        bool found = false;
        foreach (var entry in _entries)
        {
            // Check if the department matches
            if (entry.ContainsKey("department") && entry["department"] == department)
            {
                PrintABRStatus(entry);
                PrintServiceNowTeam(entry);
                if (SplitTeam(entry))
                {
                    PrintServiceNowTeamExtra(entry);
                }
                PrintFileRepoStatus(entry);
                PrintNotes(entry);
                Console.WriteLine();
                found = true;
            }
        }

        if (!found)
        {
            Console.WriteLine("No entries found for the specified department.");
        }
    }
    //generic printing method
    //TODO: Look at creating more unified function to provide printing that can be used for the various fields
    //Current fields have small quirks that need to be checked, or slight differences with desired printed output
    static void PrintEntryField(Dictionary<string, string> entry, string fieldName, string displayName)
    {
        if (entry.ContainsKey(fieldName))
            ColoredConsole.WriteLine($"{displayName.Cyan()}: {entry[fieldName].Red()}");
        else
            Console.WriteLine($"{displayName}: Not available");
    }
    //Check if for multiple team support
    static bool SplitTeam(Dictionary<string, string> entry)
    {
        if (entry.ContainsKey("team.split") && entry["team.split"] != "N")
        {
            return true;
        }
        return false;
    }

        //check for SN entry, if not print out marked entry for "team"
        static void PrintServiceNowTeam(Dictionary<string, string> entry)
    {

        if (entry.ContainsKey("SN.1") && entry.ContainsKey("SNTeam.1"))
        {
          if (entry["SN.1"] == "Y")
            { 
            ColoredConsole.WriteLine($"{Cyan("Service Now Team:")} {entry["SNTeam.1"].Red()}");
            }
        else
        {
            PrintEntryField(entry, "team.1", "Primary Support Team");
        }
        }
    }
    //Check for teams beyond first team, if exists loop through and finish printing extra teams
    static void PrintServiceNowTeamExtra(Dictionary<string, string> entry)
    {

        Int32.TryParse(entry["team.split"], out int num);
        for (int i = 2; i <= num; i++)
            {
            if (entry.ContainsKey($"SN.{i}") && entry.ContainsKey($"SNTeam.{i}"))
            {
                ColoredConsole.WriteLine($"{Cyan("Service Now Team:")} {entry[$"SNTeam.{i}"].Red()}");
            }
            else
            {
                PrintEntryField(entry, $"team.{i}", "Team");
            }
        }        
            
    }
    static void PrintABRStatus(Dictionary<string, string> entry)
    {

        if (entry.ContainsKey("ABR"))
        {
            if (entry["ABR"] == "YES")
                ColoredConsole.WriteLine($"{Cyan("ABR:")} {Green("Yes")}");
            else
                ColoredConsole.WriteLine($"{Cyan("ABR:")} {Red("No")}");
        }
    }
    static void PrintFileRepoStatus(Dictionary<string, string> entry)
    {
        if (entry.ContainsKey("fr") && entry.ContainsKey("frepoloc"))
        {
            if (entry["fr"] == "Y")
                ColoredConsole.WriteLine($"{Cyan("File Repo:")} {Green("Yes")}");
        }
    }

    static void PrintNotes(Dictionary<string, string> entry)
    {
        if (entry.ContainsKey("notes") && entry["notes"] != "N")
        {
                ColoredConsole.WriteLine($"{Cyan("Notes: ")} {entry["notes"].Red()}");
        }
    }

    public static void FREntry(string department)
    {
        bool found = false;
        foreach (var entry in _entries)
        {
            // Check if the department matches
            if (entry.ContainsKey("department") && entry["department"] == department)
            {
                if (entry.ContainsKey("fr") && entry["fr"] == "Y")
                {
                    HTTP.OpenURL(entry["frepoloc"]);
                    found = true;
                    break;
                }
            }
        }
        if (!found)
        {
            Console.WriteLine($"No File repository for {department}");
            Console.WriteLine();
        }
    }
    
}
