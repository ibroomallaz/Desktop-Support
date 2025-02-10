using Colors.Net;
using static Colors.Net.StringStaticMethods;

public partial class DSTools

{

    public static void DSToolsMenu()
    {
        bool tlcMenu = true;
        Console.Clear();
        while (tlcMenu)
        {
            //more to be added later if thought of
            Console.WriteLine("Desktop Support Tools Menu:\n");
            Console.WriteLine("Select option:");
            ColoredConsole.WriteLine($"({Green("1")}) Quick Links");
            ColoredConsole.WriteLine($"({DarkYellow("2")}) MIM Group Check");
            ColoredConsole.WriteLine($"({Cyan("3")}) NetID lookup from Employee/Student ID number\n");
            ColoredConsole.WriteLine($"At any time: type '{Red("exit")}' to go back to main menu");
            string tlcMenuAnswer = Console.ReadLine().ToLower().Trim();

            switch (tlcMenuAnswer)
            {
                case "1":
                    tlcMenu = false;
                    Console.Clear();
                    QuickLinks.QLMain();
                    break;
                case "2":
                    tlcMenu = false;
                    Console.Clear();
                    MIMCheck.MIMCheckMenu();
                    break;
                case "back":
                case "exit":
                    tlcMenu = false;
                    Console.Clear();
                    Menus.MainMenu();
                    break;
                default:
                    tlcMenu = true;
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "3":
                    Console.Clear();
                    tlcMenu = false;
                    UserLookupByNumber();
                    break;
            }

        }
    }
    
    //retiring for now but keeping code in place
    public static void OnboardMenu()
    {
        bool onboardMenu = true;
        Console.Clear();
        Console.WriteLine("Computer Onboarding Assistant.");
        ColoredConsole.WriteLine($"At any time: type '{DarkYellow("back")}' to move back one menu, '{Cyan("clear")}' to clear the text, '{Red("exit")}' to go back to main menu");
        while (onboardMenu)
        {
            ColoredConsole.WriteLine($"Is the computer running {Cyan("Windows")} or {Red("macOS")}?");
            string onboardMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (onboardMenuAnswer)
            {
                case "1":
                case "windows":
                case "win":
                case "w":
                    onboardMenu = false;
  //                  WinOnboard.WinOnboardMenu();
                    break;
                case "2":
                case "mac":
                case "macos":
                case "m":
                    onboardMenu = false;
    //                MacOnboard.MacOnboardMenu();
                    break;
                case "back":
                    onboardMenu = false;
                    DSToolsMenu();
                    break;
                case "exit":
                    onboardMenu = false;
                    Console.Clear();
                    Menus.MainMenu();
                    break;
                default:
                    break;
                case "clear":
                    Console.Clear();
                    break;
            }
        }
    }

    static void  UserLookupByNumber()
    {
        Console.Clear();
        bool userLBN = true;
        while (userLBN)
        {
            ColoredConsole.WriteLine($"User Information: Enter Employee or StudentID, {Cyan("clear")}, {DarkYellow("back")}, {Green("help")} or {DarkRed("exit")}:");
            string userLBNText = Console.ReadLine().ToLower().Trim();

            switch (userLBNText)
            {
                case "clear":
                    Console.Clear();
                    break;
                case "back":
                    Console.Clear();
                    userLBN = false;
                    DSTools.DSToolsMenu();
                    break;
                case "exit":
                    userLBN = false;
                    Menus.MainMenu();
                    break;
                case "":
                    break;
                default:
                    Console.WriteLine();
//                    AD.ADUserFromNumber(userLBNText);
                    Console.WriteLine();
                    break;

            }
        }
    }
}
