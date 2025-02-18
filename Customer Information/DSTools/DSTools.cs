using Colors.Net;
using static Colors.Net.StringStaticMethods;

public partial class DSTools

{

    public static async Task DSToolsMenu()
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
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }
            string tlcMenuAnswer = input.ToLower().Trim();


            switch (tlcMenuAnswer)
            {
                case "1":
                    tlcMenu = false;
                    Console.Clear();
                    await QuickLinks.QLMainMenu();
                    break;
                case "2":
                    tlcMenu = false;
                    Console.Clear();
                    await MIMCheck.MIMCheckMenu();
                    break;
                case "back":
                case "exit":
                    tlcMenu = false;
                    Console.Clear();
                    await Menus.MainMenu();
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
                    await UserLookupByNumber();
                    break;
            }

        }
    }
    
 

    static async Task  UserLookupByNumber()
    {
        Console.Clear();
        bool userLBN = true;
        while (userLBN)
        {
            ColoredConsole.WriteLine($"User Information: Enter Employee or StudentID, {Cyan("clear")}, {DarkYellow("back")}, {Green("help")} or {DarkRed("exit")}:");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }
            string userLBNText = input.ToLower().Trim();

            switch (userLBNText)
            {
                case "clear":
                    Console.Clear();
                    break;
                case "back":
                    Console.Clear();
                    userLBN = false;
                    await DSTools.DSToolsMenu();
                    break;
                case "exit":
                    userLBN = false;
                    await Menus.MainMenu();
                    break;
                case "":
                    break;
                default:
                    Console.WriteLine();
                    await ADUserInfo.UserFromNumber(userLBNText);
                    Console.WriteLine();
                    break;

            }
        }
    }
}
