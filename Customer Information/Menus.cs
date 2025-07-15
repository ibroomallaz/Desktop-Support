using Colors.Net;
using Colors.Net.StringColorExtensions;
using Microsoft.Extensions.DependencyInjection;
using static Colors.Net.StringStaticMethods;

class Menus
{
    public static async Task MainMenu()
    {
        bool showMenu = true;
        while (showMenu)
        {
            ColoredConsole.WriteLine($"UITS Desktop Support App ({Red("ver.")} {Globals.g_AppVersion.Cyan()})\n");
            Console.WriteLine("Choose an option:");
            ColoredConsole.WriteLine($"({Green("1")}) User Info:");
            ColoredConsole.WriteLine($"({DarkYellow("2")}) Computer Info:");
            ColoredConsole.WriteLine($"({Cyan("3")}) About:");
            ColoredConsole.WriteLine($"({Magenta("4")}) Desktop Support Tools:");
            ColoredConsole.WriteLine($"({DarkRed("5")}) Quit");
            Console.Write("\r\nSelect an option: ");
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            string input = Console.ReadLine();
#pragma warning restore CS8600
            if (string.IsNullOrEmpty(input))
            {
                Console.Clear();
                continue;
            }
            string menuInput = input.Trim().ToLower(); 
            switch (menuInput)
            {
                case "1":
                case "user":
                    showMenu = false;
                    await UserInfoMenu();
                    break;
                case "2":
                case "computer":
                    showMenu = false;
                    await ComputerInfoMenu();
                    break;
                case "3":
                case "about":
                    showMenu = false;
                    await AboutMenu();
                    break;
                case "5":
                case "quit":
                    showMenu = false;
                    break;
                case "4":
                    showMenu = false;
                    await DSTools.DSToolsMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    Console.Clear();
                    ColoredConsole.WriteLine($"{Red("Please enter a valid option")}\n");
                    break;


            }
        }
    }
    public static async Task AboutMenu()
    {
        Console.Clear();
        ColoredConsole.WriteLine($"UITS Desktop Support App \n {Red("Version")} {Application.ProductVersion} \n Developed by Isaac {Cyan("Broomall")} ({Green("i")}{Cyan("broomall")})\n Press {DarkYellow("Enter")} to go back.");
        Console.ReadLine();
        Console.Clear();
        await MainMenu();
    }
    public static async Task UserInfoMenu()
    {
        Console.Clear();
        bool userMenu = true;
        while (userMenu)
        {
            ColoredConsole.WriteLine($"User Information: Enter NetID, {Cyan("clear")}, {DarkYellow("back")}, {Green("help")} or {DarkRed("exit")}:");
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            string userMenuText = Console.ReadLine().ToLower().Trim();
#pragma warning restore CS8602

            switch (userMenuText)
            {
                case "clear":
                    Console.Clear();
                    break;
                case "back":
                    Console.Clear();
                    userMenu = false;
                    await MainMenu();
                    break;
                case "exit":
                    userMenu = false;
                    break;
                case "-reload":
                    try
                    {
                        await Globals.DepartmentService.ReloadDataAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex}. Data not reloaded properly. Please restart app.");
                    }
                    break;
                case "-cl":
                    HTTP.OpenURL("https://emailarizona.sharepoint.com/:x:/r/sites/UITS-DesktopSupport/Shared%20Documents/General/Customer%20List%20by%20Core%20Support%20Team.xlsx?d=w086768426f3745cda79987cc374d1ed5&csf=1&web=1&e=SbhIsJ");
                    ColoredConsole.WriteLine($"{Green("Opening")} Customer List by Core Support Team");
                    break;
                case "-ps":
                    HTTP.OpenURL("https://emailarizona.sharepoint.com/:w:/r/sites/UITS-DesktopSupport/Shared%20Documents/General/DST%20TS-Phone%20Coverage.docx?d=w080a9cc0f66c4a9baa10e7f9eeed418f&csf=1&web=1&e=Mre5jG");
                    ColoredConsole.WriteLine($"{Green("Opening")} Desktop Support Phone Schedule");
                    break;
                case "help":
                    await HiddenMenu();
                    break;
                default:
                    ADUserInfo ADUser = new ADUserInfo(userMenuText);
                    if (ADUser.Exists)
                    {
                        await ADUserInfo.PrintADUserInfo(ADUser);
                    }
                    else if (!string.IsNullOrWhiteSpace(ADUser.ErrorMessage))
                    {
                        ColoredConsole.WriteLine($"{Red("Error")}: {ADUser.ErrorMessage}\n");
                    }
                    else
                    {
                        ColoredConsole.WriteLine($"{DarkYellow(userMenuText)} is {DarkRed("not")} a Valid NetID\n");
                    }


                    break;
            }
        }
    }
    public static async Task ComputerInfoMenu()
    {
        Console.Clear();
        bool computerMenu = true;
        while (computerMenu)
        {
            ColoredConsole.WriteLine($"Computer Information: Enter Hostname, {Cyan("clear")}, {DarkYellow("back")}, or {DarkRed("exit")}:");
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            string computerMenuText = Console.ReadLine().ToLower().Trim();
#pragma warning restore CS8602

            switch (computerMenuText)
            {
                case "clear":
                    Console.Clear();
                    break;
                case "back":
                    Console.Clear();
                    computerMenu = false;
                    await MainMenu();
                    break;
                case "exit":
                    computerMenu = false;
                    break;
                case "":
                    break;
                default:
                    ADComputer ADComputer = new ADComputer(computerMenuText);
                    if (ADComputer.Exists)
                    {
                        await ADComputer.PrintADComputerInfo(ADComputer);
                    }
                    else
                    {
                        ColoredConsole.WriteLine($"{DarkYellow(computerMenuText)} is {DarkRed("not")} in BlueCat.\n");  //Print computer failure
                    }

                    break;
            }
        }
    }
    static Task HiddenMenu()
    {
       
        ColoredConsole.WriteLine($"Unlisted functions and options:");
        ColoredConsole.WriteLine($"\"{Cyan("-cl")}\": {DarkYellow("Customer List")} - Pulls up Desktop Support Customer list Sharepoint document.");
        ColoredConsole.WriteLine($"\"{Cyan("-reload")}\": {DarkYellow("Reloads")} departmental data in the event that data isn't updating or loading properly.");
        ColoredConsole.WriteLine($"\"{Cyan("-ps")}\": {DarkYellow("Phone Schedule")} - Opens the Desktop Support Phone schedule.");
        return Task.CompletedTask;
    }
}