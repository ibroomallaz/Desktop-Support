﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class DepartmentList
{
    public string? Exception { get; set; }

    public static async Task GetInfo(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch (Exception e)
        {
            //this.Exception = e.ToString();
            Console.WriteLine($"The process failed: {e}");
        }


        await HTTP.DownloadFile(Globals.g_DepartmentJSONURL, Globals.g_DepartmentJSONPath);
      
    }

    public static async Task<IDepartment?> ReadJson()
    {
        if (!File.Exists(Globals.g_DepartmentJSONPath))
        {
            Console.WriteLine("Department data missing. Attempting to redownload...");
            await GetInfo(Globals.g_DepartmentJSONPath);
        }

        using StreamReader reader = new StreamReader(Globals.g_DepartmentJSONPath);
        string json = await reader.ReadToEndAsync();

        try
        {
            return JsonConvert.DeserializeObject<Department>(json);
        }
        catch (Exception e)
        {
             Console.WriteLine(e.ToString());
            return null;
        }
    }
}
public interface IDepartment
{
    string Number { get; }
    bool SupportKnown { get; }
    bool SplitSupport { get; }
    Team? Team1 { get; }
    Team? Team2 { get; }
    FileRepo? Filerepo { get; }
    string? Notes { get; }
}
public class Department : IDepartment
{
    public required string Number { get; set; }
    public bool SupportKnown { get; set; }
    public bool SplitSupport { get; set; }
    public Team? Team1 { get; set; }
    public Team? Team2 { get; set; }
    public FileRepo? Filerepo { get; set; }
    public string? Notes { get; set; }
}

public class Team
{
    public required string Name { get; set; }
    public bool ServiceNow { get; set; }
}

public class FileRepo
{
    public bool Exists { get; set; }
    public string? Location { get; set; }
}
