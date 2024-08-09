using Microsoft.VisualBasic.Logging;
using System;
using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;

public class MacOnboard
{
    //despite many sections of the flow without branching, coding through switch statements in case of pulling up/using help articles
    static Stack<Action> macMenuStack = new Stack<Action>();
    public static void MacOnboardMenu()
    {
        macMenuStack.Clear();
        macMenuStack.Push(MacOnboardMenu);
        bool macOnboardMenu = true;
        while (macOnboardMenu)
        {
            ColoredConsole.WriteLine($"Is there an AppleID signed into the device ({Green("Y")}/{DarkRed("N")})?");
            string macOnboardMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (macOnboardMenuAnswer)
            {
                case "y":
                case "yes":
                    ColoredConsole.WriteLine($"Remove the device from FindMyMac and sign out of Apple ID. \n Press {DarkYellow("Enter")} to Continue.");
                    Console.ReadLine();
                    macOnboardMenu = false;
                    MacUserDataMenu();
                    break;
                case "n": case "no":
                    macOnboardMenu = false;
                    MacUserDataMenu();
                    break;
                case "back":
                    macOnboardMenu = false;
                    DSTools.OnboardMenu();
                    macMenuStack.Clear();
                    break;
                case "exit":
                    macOnboardMenu = false;
                    Menus.MainMenu();
                    macMenuStack.Clear();
                    break;
                default:
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://support.apple.com/guide/mac-help/sign-in-or-out-of-your-apple-id-on-mac-mchla99dc8da/mac");
                    macOnboardMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }

        }
    }
    public static void MacUserDataMenu()
    {
        bool macUserDataMenu = true;
        macMenuStack.Push(MacUserDataMenu);
        while(macUserDataMenu)
        {
            ColoredConsole.WriteLine($"Is there important user data on the device ({Green("Y")}/{DarkRed("N")})?");
           string macUserDataMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch(macUserDataMenuAnswer)
            {
                case "y": case "yes":
                    macUserDataMenu = false;
                    MacAdminMenu();
                    break;
                case "n": case "no":
                    macUserDataMenu= false;
                    MacReimageMenu();                                                                                                                
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacUserDatMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit":
                    macUserDataMenu = false;
                    macMenuStack.Clear();
                    Menus.MainMenu();
                    break;
                default:
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://uarizona.service-now.com/sp?id=kb_article_view&sysparm_article=KB0011356");
                    macUserDataMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }
    }
    public static void MacAdminMenu()
    {
        bool macAdminMenu = true;
        macMenuStack.Push(MacAdminMenu);
        while(macAdminMenu)
        {
            ColoredConsole.WriteLine($"Is there an administrator account on the device ({Green("Y")}/{DarkRed("N")})?");
            string macAdminMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch(macAdminMenuAnswer)
            {
                case "y": case "yes":
                    ColoredConsole.WriteLine($"Log in as the Administrator.\n Press {DarkYellow("Enter")} to continue.");
                    macAdminMenu = false;
                    MacUEMAllowMenu();
                    break;
                case "n": case "no":
                    ColoredConsole.WriteLine($"Create an Administrator account.\n Press {DarkYellow("Enter")} to continue.");
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacAdminMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit": macAdminMenu = false;
                    macMenuStack.Clear();
                    Menus.MainMenu();
                    break;
                default:
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://support.apple.com/guide/mac-help/add-a-user-or-group-mchl3e281fc9/14.0/mac/14.0");
                    macAdminMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }
    }
 
    public static void MacUEMAllowMenu()
    {
        bool macUEMAllowMenu = true;
        macMenuStack.Push(MacUEMAllowMenu);
        while(macUEMAllowMenu)
        {
            ColoredConsole.WriteLine($"Allow the device in UEM. \n Press {DarkYellow("Enter")} to continue.");
            string macUEMAllowMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch(macUEMAllowMenuAnswer)
            {
                default:
                    macUEMAllowMenu = false;
                    MacDlWS1Menu();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacUEMAllowMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit":
                    macUEMAllowMenu = false;
                    macMenuStack.Clear();
                    Menus.MainMenu();
                    break;
                case "clear":
                    break;
                case "help":
                    HTTP.OpenURL("https://uarizona.service-now.com/sp?id=kb_article_view&sysparm_article=KB0011356");
                    macUEMAllowMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }
    }
    public static void MacReimageMenu()
    {
        bool macReimageMenu = true;
        macMenuStack.Push(MacReimageMenu);
        while(macReimageMenu)
        {
            ColoredConsole.WriteLine($"Re-image the device. \n Press {DarkYellow("Enter")} to continue.");
            string macReimageMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch(macReimageMenuAnswer)
            {
                default:
                    macReimageMenu = false;
                    MacMDMEnrollMenu();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacReimageMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit":
                    macMenuStack.Clear();
                    macReimageMenu = false;
                    Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://support.apple.com/en-us/102655");
                    macReimageMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }

    }
    public static void MacMDMEnrollMenu()
    {
        bool macMDMEnrollMenu = true;
        macMenuStack.Push(MacMDMEnrollMenu);
        while (macMDMEnrollMenu)
        {
            ColoredConsole.WriteLine($"Enroll the computer into MDM using Apple Configurator.\n Press {DarkYellow("Enter")} to continue.");
            string macMDMEnrollMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch(macMDMEnrollMenuAnswer)
            {
                default:
                    macMDMEnrollMenu = false;
                    MacASMAssignMenu();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacMDMEnrollMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit":
                    macMenuStack.Clear();
                    macMDMEnrollMenu = false;
                    Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://uarizona.service-now.com/sp?id=kb_article_view&sysparm_article=KB0011734");
                    macMDMEnrollMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }
    }
    public static void MacASMAssignMenu()
    {
        bool macASMAssignMenu = true;
        macMenuStack.Push(MacASMAssignMenu);
        while (macASMAssignMenu)
        {
            ColoredConsole.WriteLine($"Assign the computer to the MDM in Apple School Manager.\n Press {DarkYellow("Enter")} to continue");
            string macASMAssignMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch(macASMAssignMenuAnswer)
            {
                default:
                    macASMAssignMenu = false;
                    MacSyncWS1Menu();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacASMAssignMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit":
                    macMenuStack.Clear();
                    macASMAssignMenu = false;
                    Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://uarizona.service-now.com/sp?id=kb_article_view&sysparm_article=KB0011734");
                    macASMAssignMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }
    }
    public static void MacSyncWS1Menu()
    {
        bool macSyncWS1Menu = true;
        macMenuStack.Push(MacSyncWS1Menu);
        while (macSyncWS1Menu)
        {
            ColoredConsole.WriteLine($"Sync the computer in WS1 UEM.\n Press {DarkYellow("Enter")} to continue.");
            string macSyncWS1MenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (macSyncWS1MenuAnswer)
            {
                default:
                    macSyncWS1Menu = false;
                    MacUserSetupDone();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacSyncWS1Menu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit":
                  macMenuStack.Clear();
                  macSyncWS1Menu= false;
                Menus.MainMenu(); break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://uarizona.service-now.com/sp?id=kb_article_view&sysparm_article=KB0011734");
                    macSyncWS1MenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }
    }

    public static void MacDlWS1Menu()
    {
        bool macDlWS1Menu = true;
        macMenuStack.Push(MacDlWS1Menu);
        while(macDlWS1Menu)
        {
            ColoredConsole.WriteLine($"Download WS1 Hub. \n Press {DarkYellow("Enter")} to continue.");
            string macDlWS1MenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (macDlWS1MenuAnswer)
            {
                default:
                    macDlWS1Menu = false;
                    MacUserSignInMenu();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacDlWS1Menu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit":
                    macDlWS1Menu = false;
                    macMenuStack.Clear();
                    Menus.MainMenu(); break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://www.getwsone.com/");
                    macDlWS1MenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;

            }
        }
    }
    public static void MacUserSignInMenu()
    {
        bool macUserSignInMenu = true;
        macMenuStack.Push(MacUserSignInMenu);
        while(macUserSignInMenu)
        {
            ColoredConsole.WriteLine($"Have the user sign into the Hub.\n Press {DarkYellow("Enter")} to continue.");
            string macUserSignInMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (macUserSignInMenuAnswer)
            {
                default:
                    macUserSignInMenu = false;
                    MacBCSignInMenu();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacUserSignInMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke(); break;
                case "exit":
                    macUserSignInMenu = false;
                    macMenuStack.Clear(); Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
 break;
                case "help":
                    HTTP.OpenURL("https://uarizona.service-now.com/sp?id=kb_article_view&sysparm_article=KB0011340");
                    macUserSignInMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }
    }
    public static void MacBCSignInMenu()
    {
        bool macBCSignInMenu = true;
        macMenuStack.Push(MacBCSignInMenu);
        while(macBCSignInMenu)
        {
            ColoredConsole.Write($"Install profiles and have the customer sign into BlueCat. \n Press {DarkYellow("Enter")} to continue.");
            string macBCSignInMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (macBCSignInMenuAnswer)
            {
                default:
                    macBCSignInMenu = false;
                    MacUserPWSyncMenu();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacBCSignInMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke();
                    break;
                case "exit":
                    macBCSignInMenu = false;
                    macMenuStack.Clear(); Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://uarizona.service-now.com/sp?id=kb_article_view&sysparm_article=KB0011356");
                    macBCSignInMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;

            }
        }
    }
    public static void MacUserPWSyncMenu()
    {
        bool macUserPWSyncMenu = true;
        macMenuStack.Push(MacUserPWSyncMenu);
        while(macUserPWSyncMenu)
        {
            ColoredConsole.WriteLine($"Have the customer sync their BlueCat Password.\n Press {DarkYellow("Enter")} to continue.");
            string macUserPWSyncMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (macUserPWSyncMenuAnswer)
            {
                default:
                    macUserPWSyncMenu = false;
                    MacUserSetupDone();
                    break;
                case "back":
                    macMenuStack.Pop();
                    if (macMenuStack.Peek().Method.Name == "MacUserPWSyncMenu")
                    {
                        macMenuStack.Pop();
                    }
                    macMenuStack.Peek().Invoke(); break;
                case "exit":
                    macUserPWSyncMenu = false;
                    macMenuStack.Clear(); Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "help":
                    HTTP.OpenURL("https://uarizona.service-now.com/sp?id=kb_article_view&sysparm_article=KB0011356");
                    macUserPWSyncMenuAnswer = Console.ReadLine().ToLower().Trim();
                    break;

            }
        }
    }
    public static void MacUserSetupDone()
    {
        bool macUserSetupDone = true;
        while (macUserSetupDone)
        {
            ColoredConsole.WriteLine($"{Green("Onboarding Process finished!")} Please begin device setup with the customer.");
            string macUserSetupDoneAnswer = Console.ReadLine().ToLower().Trim();
            switch (macUserSetupDoneAnswer)
            {
                default:
                    macUserSetupDone = false;
                    macMenuStack.Clear();
                    Menus.MainMenu();
                    break;
                case "back":
                    macUserSetupDone = false;
                    macMenuStack.Peek().Invoke();
                    macMenuStack.Pop();
                    break;
                case "exit":
                    macMenuStack.Clear();
                    macUserSetupDone = false;
                    Menus.MainMenu();
                    break;
                case "help":
                    HTTP.OpenURL("https://emailarizona.sharepoint.com/:w:/r/sites/UITS-DesktopSupport/Shared%20Documents/General/TLC%20Customer%20Checklist_2pg.docx");
                    macUserSetupDoneAnswer = Console.ReadLine().ToLower().Trim();
                    break;
            }
        }
    }
}
