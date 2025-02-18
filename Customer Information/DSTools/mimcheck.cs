using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;


public class MIMCheck
{
    public static async Task MIMCheckMenu()
    {
        bool mimCheckMenu = true;
        while (mimCheckMenu)
        {
            ColoredConsole.WriteLine($"MIM Group Checking options:\n");
            ColoredConsole.WriteLine($"({Green("1")}) Report individual's current MIM Groups.");
            ColoredConsole.WriteLine($"({DarkYellow("2")}) Check current members of Dept MIM group.");
            ColoredConsole.WriteLine($"({DarkRed("3")}) Exit back to Main Menu");
            string mimCheckMenuAnswer = Console.ReadLine().Trim().ToLower();
            switch (mimCheckMenuAnswer)
            {
                case "1":
                    mimCheckMenu = false;
                    Console.Clear();
                    CurrentMIMMenu();
                    break;
                case "2":
                    mimCheckMenu = false;
                    Console.Clear();
                    await ListMIMMenu();
                    break;
                case "exit":
                case "3":
                    mimCheckMenu = false;
                    Console.Clear();
                    await Menus.MainMenu();
                    break;
                case "back":
                    Console.Clear();
                    mimCheckMenu = false;
                    await DSTools.DSToolsMenu();
                    break;
                default:
                    break;

            }
        }

    }

    public static async Task ListMIMMenu()
    {
        bool listMIMMEnu = true;
        while (listMIMMEnu)
        {
            ColoredConsole.WriteLine($"Input Deparmtnet Number MIM group you wish to check, {DarkYellow("back")}, {Cyan("clear")}, or {DarkRed("exit")}:");
            string listMIMMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (listMIMMenuAnswer)
            {
                case "back":
                    listMIMMEnu = false;
                    MIMCheckMenu();
                    break;
                case "exit":
                    listMIMMEnu = false;
                    Console.Clear();
                    await Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                  default:
                    string dept = "UA-MIM-0" + listMIMMenuAnswer;
                    ADGroup group = new ADGroup(dept);
                    if (!group.Exists)
                    {
                        ColoredConsole.WriteLine($"MIM Group {DarkGreen(dept)} does not exist.");
                    }
                    else
                    {                      
                        Console.WriteLine("Total group members: " + group.MemberCount);
                        foreach (var member in group.GroupMembers)
                        {
                            Console.WriteLine(member);
                        }
                    }
                    break;
            }
        }
    }

    public static async Task CurrentMIMMenu()
    {
        bool currentMIMMenu = true;
        while (currentMIMMenu)
        {
            ColoredConsole.WriteLine($"Check current MIM groups:");
            ColoredConsole.WriteLine($"Enter {DarkGreen("Netid")}, {DarkYellow("Back")}, {Cyan("Clear")}, or {DarkRed("Exit")}:");
            string currentMIMMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (currentMIMMenuAnswer)
            {
                case "back":
                    currentMIMMenu = false;
                    Console.Clear();
                    MIMCheckMenu();
                    break;
                case "exit":
                    currentMIMMenu = false;
                    Console.Clear();
                    await Menus.MainMenu();
                    break;
                case "":
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    ADUserInfo user = new ADUserInfo(currentMIMMenuAnswer);
                    user.GetADMIMGroups(currentMIMMenuAnswer);
                    bool mimExists = user.MimGroupExists ?? false;
                    Console.WriteLine();
                    if (!user.Exists)
                    {
                        Console.WriteLine($"{currentMIMMenuAnswer} is not a Valid NetID");
                        Console.WriteLine();
                        break;
                    }
                    if (user.Exists && mimExists)
                    {
                        Console.WriteLine(); //Asthetics
                        foreach (var group in user.MimGroupsList)
                        {
                            Console.WriteLine(group);
                        }

                    }
                    else
                    {
                        Console.WriteLine($"No valid MIM groups found for {currentMIMMenuAnswer}");
                    }
                    Console.WriteLine();
                    break;
            }

        }
    }

}