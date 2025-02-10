using Colors.Net;
using Colors.Net.StringColorExtensions;
using Microsoft.Extensions.DependencyInjection;
using static Colors.Net.StringStaticMethods;

class Menus
{
    public static void MainMenu()
    {
        bool showMenu = true;
        while (showMenu)
        {
            ColoredConsole.WriteLine($"UITS Desktop Support App ({Red("ver.")} {Application.ProductVersion.Cyan()})\n");
            Console.WriteLine("Choose an option:");
            ColoredConsole.WriteLine($"({Green("1")}) User Info:");
            ColoredConsole.WriteLine($"({DarkYellow("2")}) Computer Info:");
            ColoredConsole.WriteLine($"({Cyan("3")}) About:");
            ColoredConsole.WriteLine($"({Magenta("4")}) Desktop Support Tools:");
            ColoredConsole.WriteLine($"({DarkRed("5")}) Quit");
            Console.Write("\r\nSelect an option: ");

            switch (Console.ReadLine().Trim().ToLower())
            {
                case "1":
                case "user":
                    showMenu = false;
                    UserInfoMenu();
                    break;
                case "2":
                case "computer":
                    showMenu = false;
                    ComputerInfoMenu();
                    break;
                case "3":
                case "about":
                    showMenu = false;
                    AboutMenu();
                    break;
                case "5":
                case "quit":
                    showMenu = false;
                    break;
                case "4":
                    showMenu = false;
                    DSTools.DSToolsMenu();
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
        ColoredConsole.WriteLine($"UITS Desktop Support App \n {Red("Version")} {Application.ProductVersion} \n Developed by Isaac {Cyan("Broomall")} ({Green("i")}{Cyan("broomall")})\n Press {DarkYellow("Enter")} to go back.");
        Console.ReadLine();
        Console.Clear();
        MainMenu();
    }
    public static async void UserInfoMenu()
    {
        Console.Clear();
        bool userMenu = true;
        while (userMenu)
        {
            ColoredConsole.WriteLine($"User Information: Enter NetID, {Cyan("clear")}, {DarkYellow("back")}, {Green("help")} or {DarkRed("exit")}:");
            string userMenuText = Console.ReadLine().ToLower().Trim();

            switch (userMenuText)
            {
                case "clear":
                    Console.Clear();
                    break;
                case "back":
                    Console.Clear();
                    userMenu = false;
                    MainMenu();
                    break;
                case "exit":
                    userMenu = false;
                    break;
                case "":
                    break;
                default:
                    ADUserInfo ADUser = new ADUserInfo(userMenuText);
                    if (ADUser.Exists)
                    {
                        ADUserInfo.PrintADUserInfo(ADUser);
                    }
                    else
                    {
                        Console.WriteLine($"{userMenuText} is not a Valid NetID");
                    }

                    break;
                case "-reload":
                    try
                    {

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex}. Data not reloaded properly. Please restart app.");
                    }
                    break;
 /*               case "-fr":
                    if (AD.adDeptStack.Peek() != null)
                    {
                        CSV.FREntry(AD.adDeptStack.Peek());
                    }
                    break; */
                case "-cl":
                    HTTP.OpenURL("https://emailarizona.sharepoint.com/:x:/r/sites/UITS-DesktopSupport/Shared%20Documents/General/Customer%20List%20by%20Core%20Support%20Team.xlsx?d=w086768426f3745cda79987cc374d1ed5&csf=1&web=1&e=SbhIsJ");
                    ColoredConsole.WriteLine($"{Green("Opening")} Customer List by Core Support Team");
                    break;
                case "-ps":
                    HTTP.OpenURL("https://emailarizona.sharepoint.com/:w:/r/sites/UITS-DesktopSupport/Shared%20Documents/General/DST%20TS-Phone%20Coverage.docx?d=w080a9cc0f66c4a9baa10e7f9eeed418f&csf=1&web=1&e=Mre5jG");
                    ColoredConsole.WriteLine($"{Green("Opening")} Desktop Support Phone Schedule");
                    break;
                case "help":
                    HiddenMenu();
                    break;
            }
        }
    }
    public static void ComputerInfoMenu()
    {
        Console.Clear();
        bool computerMenu = true;
        while (computerMenu)
        {
            ColoredConsole.WriteLine($"Computer Information: Enter Hostname, {Cyan("clear")}, {DarkYellow("back")}, or {DarkRed("exit")}:");
            string computerMenuText = Console.ReadLine().ToLower().Trim();

            switch (computerMenuText)
            {
                case "clear":
                    Console.Clear();
                    break;
                case "back":
                    Console.Clear();
                    computerMenu = false;
                    MainMenu();
                    break;
                case "exit":
                    computerMenu = false;
                    break;
                case "":
                    break;
                default:
                    Console.WriteLine();
 //                   AD.ADComputer(computerMenuText);
                    Console.WriteLine();
                    break;
            }
        }
    }
    static void HiddenMenu()
    {
       
        ColoredConsole.WriteLine($"Unlisted functions and options:");
        ColoredConsole.WriteLine($"\"{Cyan("-cl")}\": {DarkYellow("Customer List")} - Pulls up Desktop Support Customer list Sharepoint document.");
        ColoredConsole.WriteLine($"\"{Cyan("-fr")}\": {DarkYellow("File Repository")} - Opens Department specific repository after a search shows one is available.");
        ColoredConsole.WriteLine($"\"{Cyan("-reload")}\": {DarkYellow("Reloads")} csv file in the event that data isn't updating or loading properly.");
        ColoredConsole.WriteLine($"\"{Cyan("-ps")}\": {DarkYellow("Phone Schedule")} - Opens the Desktop Support Phone schedule.");

    }
}