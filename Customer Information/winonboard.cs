using Microsoft.VisualBasic.Devices;
using System;
using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;
public class WinOnboard
{
    /*
     */

    static Stack<Action> menuStack = new Stack<Action>();
    public static void WinOnboardMenu()
    {
        bool winOnboardMenu = true;
        ColoredConsole.WriteLine($"Is the computer at least on Windows 10 ({Green("Y")}/{DarkRed("N")})?");
        string winOnboardMenuAnswer = Console.ReadLine().ToLower().Trim();
        menuStack.Clear();
        menuStack.Push(WinOnboardMenu);
        while (winOnboardMenu)
        {
            switch (winOnboardMenuAnswer)
            {
                case "y":
                case "yes":
                    winOnboardMenu = false;
                    WinMenuBC();
                    break;
                case "n":
                case "no":
                    WinOnboardMenuN();
                    winOnboardMenu = false;
                    break;
                case "back":
                    winOnboardMenu = false;
                    TLCHelp.OnboardMenu();
                    menuStack.Clear(); break;
                case "exit":
                    winOnboardMenu = false;
                    menuStack.Clear();
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
    public static void WinOnboardMenuN()
    {
        //From WinOnBoardMenu()
        menuStack.Push(WinOnboardMenuN);
        bool winOnboardMenuN = true;
        ColoredConsole.WriteLine($"Can the computer be upgraded to at least Windows 10 ({Green("Y")}/{DarkRed("N")})?");
        string winOnboardMenuNAnswer = Console.ReadLine().ToLower().Trim();
        while (winOnboardMenuN)
        {
            switch (winOnboardMenuNAnswer)
            {
                case "y":
                case "yes":
                    winOnboardMenuN = false;
                    WinMenuBC();
                    break;
                case "n":
                case "no":
                    ColoredConsole.WriteLine($"Do {Red("not")} onboard the computer.");
                    Console.ReadLine();
                    winOnboardMenuN = false;
                    TLCHelp.TLCHelpMenu();
                    break;
                case "back":
                    winOnboardMenuN = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinOnboardMenuN")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winOnboardMenuN = false;
                    Program.Menu();
                    break;
                default:
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                        Program.OpenURL("https://support.microsoft.com/en-us/windows/windows-10-system-requirements-6d4e9a79-66bf-7950-467c-795cf0386715");
                    winOnboardMenuNAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }

    }
    public static void WinMenuBC()
    {
        //comes from WinOnboardMenu()        
        menuStack.Push(WinMenuBC);
        bool winMenuBC = true;
        while (winMenuBC)
        {
            ColoredConsole.WriteLine($"Is the computer currently on BlueCat ({Green("Y")}/{DarkRed("N")})?");
            string winMenuBCAnswer = Console.ReadLine().ToLower().Trim();
            switch (winMenuBCAnswer)
            {
                case "y":
                case "yes":
                    winMenuBC = false;
                    WinMenuOU();
                    break;
                case "n":
                case "no":
                    ColoredConsole.WriteLine($"Prestage the device into the proper OU in Bluecat. \nPress {DarkYellow("enter")} to continue.");
                    Console.ReadLine();
                    winMenuBC = false;
                    WinMenuUserData1();
                    break;
                case "back":
                    winMenuBC = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinMenuBC")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winMenuBC = false;
                    Program.Menu(); break;
                default:
                    break;
                case "clear":
                    Console.Clear();
                    break;
            }
        }
    }
  
    public static void WinMenuOU()
    {
        menuStack.Push(WinMenuOU);
        //comes from WinMenuBC()
        bool winMenuOU = true;
        while (winMenuOU)
        {
            ColoredConsole.WriteLine($"Is the computer in the proper OU in BlueCat ({Green("Y")}/{DarkRed("N")})");
            string winMenuOUAnswer = Console.ReadLine().ToLower().Trim();
            switch (winMenuOUAnswer)
            {
                case "y":
                case "yes":
                    winMenuOU = false;
                    WinMenuShared();
                    break;
                case "n":
                case "no":
                    winMenuOU = false;
                    WinMenuUserData2();
                    break;
                case "back":
                    winMenuOU = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinMenuOU")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winMenuOU = false;
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
    public static void WinMenuShared()
    {
        //from multiple
        menuStack.Push(WinMenuShared);
        bool winMenuShared = true;
        while(winMenuShared)
        {
            ColoredConsole.WriteLine($"Is the computer a shared device ({Green("Y")}/{DarkRed("N")})?");
            string winMenuShared1Answer = Console.ReadLine().ToLower().Trim();
            switch(winMenuShared1Answer)
            {
                case "y": case "yes":
                    ColoredConsole.WriteLine($"The computer is onboarded! Complete the Setup Checklist. Press {DarkYellow("enter")} to return to main menu.");
                    Console.ReadLine();
                    winMenuShared = false;
                    break;
                case "n": case "no":
                    winMenuShared = false;
                    WinMenuHub();
                    break;
                case "back":
                    winMenuShared = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinMenuShared")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winMenuShared = false;
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
  //Multiple WinMenuUserData functions due to slight differences between the flowchart. 1 splits from the "no" side of the Exists in BC tree. 2 from the "yes" side
    public static void WinMenuUserData1()
    {
        //comes from WinMenuBC()
        
        menuStack.Push(WinMenuUserData1);
        bool winMenuUserData1 = true;
        while(winMenuUserData1)
        {
            ColoredConsole.WriteLine($"Is there important User Data on the computer ({Green("Y")}/{DarkRed("N")}?");
            string winMenuUserData1Answer = Console.ReadLine().ToLower().Trim();
            switch (winMenuUserData1Answer)
            {
                case "y":
                case "yes":
                    ColoredConsole.WriteLine($"Perform a ProfWiz migration. Press {DarkYellow("Enter")} once completed.");
                    Console.ReadLine();
                    winMenuUserData1 = false;
                    WinMenuShared();
                    break;
                case "n": case "no":
                    ColoredConsole.WriteLine($"Join the device to the domain and run a gpupdate once completed. \n(Press {DarkYellow("Enter")} to continue).");
                    Console.ReadLine();
                    winMenuUserData1 = false;
                    WinMenuShared();
                    break;
                case "back":
                    winMenuUserData1 = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinMenuUserData1")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winMenuUserData1=false;
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
    public static void WinMenuUserData2()
    {
        //Comes from WinMenuOU
        menuStack.Push(WinMenuUserData2);
        bool winMenuUserData2 = true;
        while(winMenuUserData2)
        {
            ColoredConsole.WriteLine($"Is there important User Data on the computer ({Green("Y")}/{DarkRed("N")})?");
            string winMenuUserData2Answer = Console.ReadLine().ToLower().Trim();
            switch (winMenuUserData2Answer)
            {
                case "y": case "yes":
                    ColoredConsole.WriteLine($"Send a ticket to SSA to move the device to the proper OU. \nPress {DarkYellow("Enter")} if completed.");
                    Console.ReadLine();
                    winMenuUserData2 = false;
                    WinMenuShared();
                    break;
                case "n":
                case "no":
                    winMenuUserData2=false;
                    WinMenuBCRights();
                    break;
                case "back":
                    winMenuUserData2 = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinMenuUserData2")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winMenuUserData2 = false;
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
    public static void WinMenuBCRights()
    {
        //Comes from WinMenuUserData2()
     menuStack.Push(WinMenuBCRights);
        bool winMenuBCRights = true;
        while(winMenuBCRights)
        {
            ColoredConsole.WriteLine($"Do you have Workstation Admin PAM rights to the OU ({Green("Y")}/{DarkRed("N")})?");
            string winMenuBCRightsAnswer = Console.ReadLine().ToLower().Trim();
            switch(winMenuBCRightsAnswer)
            {
                case "y": case "yes":
                    ColoredConsole.WriteLine($"Delete the original Object in AD and re-stage the object into the proper OU. \nPress {DarkYellow("Enter")} when complete.");
                    Console.ReadLine();
                    winMenuBCRights = false;
                    WinMenuShared();
                    break;
                case "n":
                case "no":
                    ColoredConsole.WriteLine($"Send a ticket to SSA to move the object to the proper OU. \nPress {DarkYellow("Enter")} when complete.");
                    winMenuBCRights = false;
                    WinMenuShared();
                    break;
                case "back":
                    winMenuBCRights = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinMenuBCRights")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winMenuBCRights = false;
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
    public static void WinMenuHub()
    {
        menuStack.Push(WinMenuHub);
        bool winMenuHub = true;
        while(winMenuHub)
        {
            ColoredConsole.WriteLine($"After running a GPUpdate, Lock the computer and wait about 10 minutes. \n Has WS1 Hub been isntalled ({Green("Y")}/{DarkRed("N")})?");
            string winMenuHubAnswer = Console.ReadLine().ToLower().Trim();
            switch(winMenuHubAnswer)
            {
                case "y":
                case "yes":
                    break;
                case "n": case "no":
                    ColoredConsole.WriteLine($"Use LAPS to instal the WS1 Hub.\n Press {DarkYellow("Enter")} to continue.");
                    Console.ReadLine();
                    winMenuHub = false;
                    WinMenuVerifyUEM();
                    break;
                case "back":
                    winMenuHub = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinMenuHub")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winMenuHub = false;
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
    public static void WinMenuVerifyUEM()
    {
        bool winMenuVerifyUEM = true;
        while(winMenuVerifyUEM)
        {
            ColoredConsole.WriteLine($"Log into UEM in your browser. \n Are you able to verify that the serial number is registered ({Green("Y")}/{DarkRed("N")})?");
            string winMenuVerifyUEMAnswer = Console.ReadLine().ToLower().Trim();
            switch (winMenuVerifyUEMAnswer)
            {
                case "y":
                case "yes":
                    ColoredConsole.WriteLine($"The computer is onboarded! Continue with the setup checklist. \n Press {DarkYellow("Enter")} to go back to main menu.");
                    winMenuVerifyUEM = false;
                    Program.Menu();
                    break;
                case "n": case "no":
                    ColoredConsole.WriteLine($"Check with SSA to ensure that the serial number is properly registering. \n Once registered the computer is fully onboarded and you can continue with the setup checklist.\n Press {DarkYellow("Enter")} to return to main menu.");
                    winMenuVerifyUEM = false;
                    Program.Menu();
                    break;
                case "back":
                    winMenuVerifyUEM = false;
                    menuStack.Pop();
                    if (menuStack.Peek().Method.Name == "WinMenuVerifyUEM")
                    {
                        menuStack.Pop();
                    }
                    menuStack.Peek().Invoke();
                    break;
                case "exit":
                    winMenuVerifyUEM=false;
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
