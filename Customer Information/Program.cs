using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;

class Program
{
    public static void Main()
    {
        //Download CSV and load information into memory
        CSV.GetCSV();
        CSV.CSVMain();
        //Download JSON for quicklink data
        QuickLinks.GetJson();
        //Run Version check and initial processes
        Version.VersionCheck();
        Menus.MainMenu();
    }
}