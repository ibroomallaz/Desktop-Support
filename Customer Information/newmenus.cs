using System;
using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;
public class NewMenus
{
	public static void MainMenu(Department departments)
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
  //                  UserInfoMenu();
                    break;
                case "2":
                case "computer":
                    showMenu = false;
//                    ComputerInfoMenu();
                    break;
                case "3":
                case "about":
                    showMenu = false;
                    AboutMenu(departments);
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
    public static void AboutMenu(Department departments)
    {
        Console.Clear();
        ColoredConsole.WriteLine($"UITS Desktop Support App \n {Red("Version")} {Application.ProductVersion} \n Developed by Isaac {Cyan("Broomall")} ({Green("i")}{Cyan("broomall")})\n Press {DarkYellow("Enter")} to go back.");
        Console.ReadLine();
        Console.Clear();
        NewMenus.MainMenu(departments);
    }
}