﻿using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;


public class MIMCheck
{
    public static void MIMCheckMenu()
    {
        bool mimCheckMenu = true;
        while (mimCheckMenu)
        {
            ColoredConsole.WriteLine($"MIM Group Checking options:\n");
            ColoredConsole.WriteLine($"({Green("1")}) Check if NetID is in expected MIM group.");
            ColoredConsole.WriteLine($"({DarkYellow("2")}) Report individual's current MIM Groups.");
            ColoredConsole.WriteLine($"({Magenta("3")}) Check current members of Dept MIM group.");
            ColoredConsole.WriteLine($"({DarkRed("4")}) Exit back to Main Menu");
            string mimCheckMenuAnswer = Console.ReadLine().Trim().ToLower();
            switch (mimCheckMenuAnswer)
            {
                case "1":
                    mimCheckMenu = false;
                    Console.Clear();
                    ExpectedMIMMenu();
                    break;
                case "2":
                    mimCheckMenu = false;
                    Console.Clear();
                    CurrentMIMMenu();
                    break;
                case "3":
                    mimCheckMenu = false;
                    Console.Clear();
                    ListMIMMenu();
                    break;
                case "exit":
                case "4":
                    mimCheckMenu = false;
                    Console.Clear();
                    Menus.MainMenu();
                    break;
                case "back":
                    Console.Clear();
                    mimCheckMenu = false;
                    DSTools.DSToolsMenu();
                    break;
                default:
                    break;

            }
        }

    }

    public static void ListMIMMenu()
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
                    Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    string dept = "UA-MIM-0" + listMIMMenuAnswer;
                    bool deptCheck = AD.ADGroupExistsCheck(dept);
                    if (!deptCheck)
                    {
                        ColoredConsole.WriteLine($"MIM Group {DarkGreen(dept)} does not exist.");
                    }
                    else
                    {
                        List<string> groupMembers = new List<string>();
                        groupMembers = AD.ADGroupMembers(dept);
                        List<string> groupMembersSorted = new List<string>();
                        groupMembersSorted = groupMembers;
                        Console.WriteLine("Total group members: " + groupMembers.Count);
                        foreach (var member in groupMembers)
                        {
                            Console.WriteLine(member);
                        }
                        groupMembers.Clear();
                        groupMembersSorted.Clear();
                    }

                    break;
            }
        }
    }

    public static void ExpectedMIMMenu()
    {
        bool expectedMIMMenu = true;
        while (expectedMIMMenu)
        {
            ColoredConsole.WriteLine($"Enter expected {Green("Department Number")}, {DarkYellow("back")}, {Cyan("clear")}, or {DarkRed("exit")}:");
            string expectedMIMMenuAnswer = Console.ReadLine().ToLower().Trim();
            switch (expectedMIMMenuAnswer)
            {
                case "back":
                    expectedMIMMenu = false;
                    Console.Clear();
                    MIMCheckMenu();
                    break;
                case "exit":
                    expectedMIMMenu = false;
                    Console.Clear();
                    Menus.MainMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "":
                    break;
                default:
                    string expectedDept = "UA-MIM-0" + expectedMIMMenuAnswer;
                    bool expectedDeptCheck = AD.ADGroupExistsCheck(expectedDept);
                    if (!expectedDeptCheck)
                    {
                        ColoredConsole.WriteLine($"Expected MIM Group {DarkGreen(expectedDept)} does not exist.");
                    }
                    else
                    {
                        bool expectedDeptL2 = true;
                        while (expectedDeptL2)
                        {
                            Console.WriteLine("Please enter user NetIDs (up to 20 separated by commas):");
                            string netid = Console.ReadLine().Trim().ToLower();
                            switch (netid)
                            {
                                default:
                                    List<string> groupMembers = AD.ADGroupMembers(expectedDept);
                                    if (!netid.Contains(","))
                                    {
                                        if (groupMembers.Contains(netid))
                                        {
                                            ColoredConsole.WriteLine($"{DarkYellow(netid)} {Green("exists")} in {DarkGreen(expectedDept)}");
                                        }
                                        else
                                        {
                                            ColoredConsole.WriteLine($"{DarkYellow(netid)} does {Red("not")} exist in {DarkGreen(expectedDept)}");
                                        }
                                    }

                                    if (netid.Contains(","))
                                    {
                                        string[] netids = new string[19];
                                        netids = netid.Split(",");
                                        for (int i = 0; i <= (netids.Length - 1); i++)
                                        {
                                            if (groupMembers.Contains(netids[i].Trim()))
                                            {
                                                ColoredConsole.WriteLine($"{netids[i].Trim().DarkYellow()} {Green("exists")} in {expectedDept.DarkGreen()}");
                                            }
                                            else
                                            {
                                                ColoredConsole.WriteLine($"{netids[i].Trim().DarkYellow()} does {Red("not")} exist in {expectedDept.DarkGreen()}");
                                            }
                                        }
                                        Array.Clear(netids, 0, netids.Length);
                                    }
                                    expectedDeptL2 = false;
                                    break;
                                case "":
                                    break;

                            }
                        }
                    }
                    break;
            }
        }
    }
    public static void CurrentMIMMenu()
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
                    Menus.MainMenu();
                    break;
                case "":
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    AD.ADMIMGroupCheck(currentMIMMenuAnswer);
                    break;
            }

        }
    }

}