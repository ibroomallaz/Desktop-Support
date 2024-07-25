using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;

public class DSTools

{
   
    public static void DSToolsMenu()
    {
        bool tlcMenu = true;
        Console.Clear();
        while (tlcMenu)
        {
            //more to be added later if thought of
            Console.WriteLine("Desktop Support Tools Menu:");
            Console.WriteLine("Select option:");
            ColoredConsole.WriteLine($"({Green("1")}) Computer Onboarding");
            ColoredConsole.WriteLine($"({DarkYellow("2")}) MIM Group Check");
            ColoredConsole.WriteLine($"At any time: type '{Red("exit")}' to go back to main menu");
            string tlcMenuAnswer = Console.ReadLine().ToLower().Trim();

            switch (tlcMenuAnswer)
            {
                case "1":
                    tlcMenu = false;
                    OnboardMenu();
                    break;
                case "2":
                    tlcMenu = false;
                    MIMCheck.MIMCheckMenu();
                    break;
                case "back":
                case "exit":
                    tlcMenu = false;
                    Program.Menu();
                    break;
                default:
                    tlcMenu = true;
                    break;
                case "clear":
                    Console.Clear();
                    break;
            }

        }
    }
  
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
            switch(onboardMenuAnswer)
            {
                case "1":
                case "windows":
                case "win":
                case "w":
                    onboardMenu = false;
                    WinOnboard.WinOnboardMenu();
                    break;
                case "2":
                case "mac":
                case "macos":
                case "m":
                    onboardMenu = false;
                   MacOnboard.MacOnboardMenu();
                    break;
                case "back":
                    onboardMenu = false;
                    DSToolsMenu();
                    break;
                case "exit":
                    onboardMenu = false;
                    Program.Menu();
                    break;
                default:
                    break;
                case "clear":
                    Console.Clear();
                    break;
            }
        }
    }
    

}