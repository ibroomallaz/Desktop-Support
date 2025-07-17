using DSAMVVM.Core.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.Core
{
    class Program
    {
        public static async Task ConsoleMain()
        {
            try
            {
                //Download departmental data, parse JSON and cache into memory for later
                await Globals.DepartmentService.PreCacheDataAsync();
                //Run Version check and initial processes
                await VersionChecker.VersionCheck();
                Console.Clear();
                await Menus.MainMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during startup: {ex.Message}");
            }
        }
    }
}
