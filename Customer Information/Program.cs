﻿using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;

class Program
{
    public static void Main()
    {
        //Download CSV and load information into memory
        CSV.GetCSV();
        CSV.CSVMain();
        //Run Version check and initial processes
        Version.VersionCheck();
        ColoredConsole.WriteLine($"UITS Desktop Support App ({Red("ver.")} {Application.ProductVersion.Cyan()})");
        Menus.MainMenu();
    }
}