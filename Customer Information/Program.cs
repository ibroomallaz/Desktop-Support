﻿using Colors.Net;
using Colors.Net.StringColorExtensions;
using static Colors.Net.StringStaticMethods;
using System.Diagnostics;
using System.Security.Policy;
using System.DirectoryServices.AccountManagement;
class Program
{
    public static string version = "2.2.1";
    public static void Main()
    {
        //Download CSV and load information into memory
        CSV.GetCSV();
        CSV.CSVMain();
        //Run Version check and initial processes
        Version.VersionCheck(version);
        ColoredConsole.WriteLine($"UITS Desktop Support App ({Red("ver.")} {version.Cyan()})");
        Menu();
    }
    public static void Menu()
    {
        bool showMenu = true;
        while (showMenu)
        {
            Console.WriteLine("Choose an option:");
            ColoredConsole.WriteLine($"({Green("1")}) User Info:");
            ColoredConsole.WriteLine($"({DarkYellow("2")}) Computer Info:");
            ColoredConsole.WriteLine($"({Cyan("3")}) About:");
            ColoredConsole.WriteLine($"({Magenta("4")}) TLC Help:");
            ColoredConsole.WriteLine($"({DarkRed("5")}) Quit");
            Console.Write("\r\nSelect an option: ");

            switch (Console.ReadLine().Trim().ToLower())
            {
                case "1":
                case "user":
                    showMenu = false;
                    UserInfoMenu();
                    break;
                case "2":
                case "computer":
                    showMenu = false;
                    ComputerInfoMenu();
                    break;
                case "3":
                case "about":
                    showMenu = false;
                    AboutMenu();
                    break;
                case "5":
                    showMenu = false;
                    break;
                case "4": showMenu = false;
                    TLCHelp.TLCHelpMenu();
                    break;
                case "clear":
                    Console.Clear();
                    break;
                default:
                    break;
            }
        }
    }
    public static void AboutMenu()
    {
        Console.Clear();
        ColoredConsole.WriteLine($"UITS Desktop Support App \n {Red("Version")} {version} \n Developed by Isaac {Cyan("Broomall")} ({Green("i")}{Cyan("broomall")})\n Press {DarkYellow("Enter")} to go back.");
        Console.ReadLine();
        Console.Clear();
        Menu();
    }
    public static void UserInfoMenu()
    {
        Console.Clear();
        bool userMenu = true;
        while (userMenu)
        {
            ColoredConsole.WriteLine($"User Information: Enter NetID, {Cyan("clear")}, {DarkYellow("back")}, or {DarkRed("exit")}:");
            string userMenuText = Console.ReadLine().ToLower().Trim();

            switch (userMenuText)
            {
                case "clear":
                    Console.Clear();
                    break;
                case "back":
                    Console.Clear();
                    userMenu = false;
                    Menu();
                    break;
                case "exit":
                    userMenu = false;
                    break;
                case "":
                    break;
                default:
                    AD.adDeptStack.Clear(); //#fr
                    Console.WriteLine();
                    AD.ADUser(userMenuText);
                    Console.WriteLine();
                    break;
//#fr
                case "-fr":
                    if (AD.adDeptStack.Peek() != null)
                    {
                        CSV.FREntry(AD.adDeptStack.Peek());
                    }
                    break;
            }
        }
    }
    public static void ComputerInfoMenu()
    {
        Console.Clear();
        bool computerMenu = true;
        while (computerMenu)
        {
            ColoredConsole.WriteLine($"Computer Information: Enter Hostname, {Cyan("clear")}, {DarkYellow("back")}, or {DarkRed("exit")}:");
            string computerMenuText = Console.ReadLine().ToLower().Trim();

            switch (computerMenuText)
            {
                case "clear":
                    Console.Clear();
                    break;
                case "back":
                    Console.Clear();
                    computerMenu = false;
                    Menu();
                    break;
                case "exit":
                    computerMenu = false;
                    break;
                case "":
                    break;
                default:
                    Console.WriteLine();
                    AD.ADComputer(computerMenuText);
                    Console.WriteLine();
                    break;
            }
        }
    }

    public static void OpenURL(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo() { FileName = target, UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception noBrowser)
        {
            if (noBrowser.ErrorCode == -2147467259)
                MessageBox.Show(noBrowser.Message);
        }
        catch (System.Exception other)
        {
            MessageBox.Show(other.Message);
        }
    }

}