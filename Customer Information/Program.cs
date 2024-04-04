using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;
using System.Diagnostics;
using System.Security.Policy;
class Program
{
    public static string version = "2.1.0";
    public static void Main()
    {
        //Download CSV and load information into memory
        CSV.GetCSV();
        CSV.CSVMain();
        //Run Version check and initial processes
        Version.VersionCheck(version);
        ColoredConsole.WriteLine($"UITS Desktop Support App ({Red("ver.")} {version.Cyan()})");
        Menu();
    }
    public static void Menu()
    {
        bool showMenu = true;
        while (showMenu)
        {
            Console.WriteLine("Choose an option:");
            ColoredConsole.WriteLine($"({Green("1")}) User Info:");
            ColoredConsole.WriteLine($"({DarkYellow("2")}) Computer Info:");
            ColoredConsole.WriteLine($"({Cyan("3")}) About:");
            ColoredConsole.WriteLine($"({Magenta("4")}) TLC Help:");
            ColoredConsole.WriteLine($"({DarkRed("5")}) Quit");
            Console.Write("\r\nSelect an option: ");

            switch (Console.ReadLine().Trim().ToLower())
            {
                case "1":
                case "user":
                    UserInfoMenu();
                    break;
                case "2":
                case "computer":
                    ComputerInfoMenu();
                    break;
                case "3":
                case "about":
                    AboutMenu();
                    break;
                case "5":
                    showMenu = false;
                    break;
                case "4": showMenu = false;
                    TLCHelp.TLCHelpMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    break;
            }
        }
    }
    public static void AboutMenu()
    {
        Console.Clear();
        ColoredConsole.WriteLine($"UITS Desktop Support App \n {Red("Version")} {version} \n Developed by Isaac {Cyan("Broomall")} ({Green("i")}{Cyan("broomall")})\n Press {DarkYellow("Enter")} to go back.");
        Console.ReadLine();
        Console.Clear();
        Menu();
    }
    public static void UserInfoMenu()
    {
        Console.Clear();
        bool userMenu = true;
        while (userMenu)
        {
            ColoredConsole.WriteLine($"User Information: Enter NetID, {Cyan("clear")}, or {DarkYellow("back")}:");
            string userMenuText = Console.ReadLine().ToLower();
           
            if (userMenuText == "clear")
            {
                Console.Clear();
                userMenu = true;
            }
            else if (userMenuText == "back")
            {
                Console.Clear();
                userMenu = false;
                Menu();
            }
            else
            {
                Console.WriteLine();
                AD.ADUser(userMenuText);
                Console.WriteLine();
                userMenu = true;
            }
        }
    }
    public static void ComputerInfoMenu()
    {
        Console.Clear();
        bool computerMenu = true;
        while (computerMenu)
        {
            ColoredConsole.WriteLine($"Computer Information: Enter Hostname, {Cyan("clear")}, or {DarkYellow("back")}:");
            string computerMenuText = Console.ReadLine().ToLower().Trim();
            if (computerMenuText == "clear")
            {
                Console.Clear();
                computerMenu = true;

            }
            if (computerMenuText == "back")
            {
                Console.Clear();
                computerMenu = false;
                Menu();
            }
            else
            {
                Console.WriteLine();
                AD.ADComputer(computerMenuText);
                Console.WriteLine();
                computerMenu = true;
            }
        }
    }
    public static void OpenURL(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo() { FileName = target, UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception noBrowser)
        {
            if (noBrowser.ErrorCode == -2147467259)
                MessageBox.Show(noBrowser.Message);
        }
        catch (System.Exception other)
        {
            MessageBox.Show(other.Message);
        }
    }
}