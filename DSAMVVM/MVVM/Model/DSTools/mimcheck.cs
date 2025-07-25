using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;

namespace DSAMVVM.MVVM.Model.DSTools
{
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
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.Clear();
                    continue;
                }
                string mimCheckMenuAnswer = input.Trim().ToLower();
                switch (mimCheckMenuAnswer)
                {
                    case "1":
                        mimCheckMenu = false;
                        Console.Clear();
                        await CurrentMIMMenu();
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
                        Console.Clear();
                        ColoredConsole.WriteLine($"{Red("Please enter a valid option")}\n");
                        break;

                }
            }

        }
        //TODO: Move from Console method to returnable object
        public static async Task ListMIMMenu()
        {
            bool listMIMMEnu = true;
            while (listMIMMEnu)
            {
                ColoredConsole.WriteLine($"Input Deparmtnet Number MIM group you wish to check, {DarkYellow("back")}, {Cyan("clear")}, or {DarkRed("exit")}:");
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                string listMIMMenuAnswer = Console.ReadLine().ToLower().Trim();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                switch (listMIMMenuAnswer)
                {
                    case "back":
                        listMIMMEnu = false;
                        await MIMCheckMenu();
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
                        ADGroup group = new(dept);
                        if (!group.Exists)
                        {
                            ColoredConsole.WriteLine($"MIM Group {DarkYellow(dept)} does {DarkRed("not")} exist.");
                        }
                        else
                        {
                            ColoredConsole.WriteLine($"\nTotal group members: " + DarkGreen(group.MemberCount.ToString()));
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            foreach (var member in group.GroupMembers)
                            {
                                Console.WriteLine(member);
                            }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
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
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                string currentMIMMenuAnswer = Console.ReadLine().ToLower().Trim();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                switch (currentMIMMenuAnswer)
                {
                    case "back":
                        currentMIMMenu = false;
                        Console.Clear();
                        await MIMCheckMenu();
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
                        ADUserInfo user = new(currentMIMMenuAnswer);
                        user.GetADMIMGroups(currentMIMMenuAnswer);
                        bool mimExists = user.MimGroupExists ?? false;
                        Console.WriteLine();
                        if (!user.Exists)
                        {
                            ColoredConsole.WriteLine($"{DarkYellow(currentMIMMenuAnswer)} is {DarkRed("not")} a Valid NetID");  //General Failure message
                            Console.WriteLine();
                            break;
                        }
                        if (user.Exists && mimExists)
                        {
                            Console.WriteLine(); //Asthetics
#pragma warning disable CS8602 // Dereference of a possibly null reference. Will not be null here
                            foreach (var group in user.MimGroupsList)
                            {
                                Console.WriteLine(group);
                            }
#pragma warning restore CS8602

                        }
                        else
                        {
                            ColoredConsole.WriteLine($"{DarkRed("No")} valid MIM groups found for {DarkYellow(currentMIMMenuAnswer)}");
                        }
                        Console.WriteLine();
                        break;
                }

            }
        }

    }
}
