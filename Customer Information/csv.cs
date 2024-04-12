using System.Net;
using Colors.Net;
using static Colors.Net.StringStaticMethods;
using Colors.Net.StringColorExtensions;
class CSV
{
    public static List<Dictionary<string, string>> _entries;
    //Download CSV from box

    public static void GetCSV()
    {
        //Obsolete method using webClient, but simple code
        WebClient webClient = new WebClient();
        webClient.DownloadFile("https://arizona.box.com/shared/static/27qy9jc64b0cpz4l6zzeu8pnri65y4d0.csv", @"c:\temp\ci.csv");
    }
    public static void CSVMain()
    {
        string filePath = @"C:\temp\ci.csv";

        _entries = ReadCSV(filePath);

    }
    // Read CSV and store entries
    public static List<Dictionary<string, string>> ReadCSV(string filePath)
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
    //check csv for department and print valid info
    public static void PrintDepartmentInfo(string department)
    {
        bool found = false;
        foreach (var entry in _entries)
        {
            // Check if the department matches
            if (entry.ContainsKey("department") && entry["department"] == department)
            {
                PrintEntryField(entry, "ABR", "ABR");
                PrintEntryField(entry, "team", "Team");
                PrintServiceNowTeam(entry);
                Console.WriteLine();
                found = true;
            }
        }

        if (!found)
        {
            Console.WriteLine("No entries found for the specified department.");
        }
    }
    //printing method
    static void PrintEntryField(Dictionary<string, string> entry, string fieldName, string displayName)
    {
        if (entry.ContainsKey(fieldName))
            ColoredConsole.WriteLine($"{displayName.Cyan()}: {entry[fieldName].Red()}");
        else
            Console.WriteLine($"{displayName}: Not available");
    }
    //combine with PrintDepartmentInfo() later
    static void PrintServiceNowTeam(Dictionary<string, string> entry)
    {
        if (entry.ContainsKey("SN") && entry.ContainsKey("sn-team"))
        {
            if (entry["SN"] == "Y")
                ColoredConsole.WriteLine($"{Cyan("Service Now Team:")} {entry["sn-team"].Red()}");
        }
    }
    //#fr
    public static void FREntry(string department)
    {
        bool found = false;
        foreach (var entry in _entries)
        {
            // Check if the department matches
            if (entry.ContainsKey("department") && entry["department"] == department)
            {
                if (entry.ContainsKey("fr") && entry["fr"] != null)
                {
                    Program.OpenURL(entry["fr"]);
                    found = true;
                }
                else
                {
                    Console.WriteLine($"No File repository for {department}");
                }
            }
        }

        if (!found)
        {
            Console.WriteLine("No entry found for the specified department.");
        }
    }
}
