class Program
{
    public static async Task Main()
    {
        try
        {
            //Download departmental data, parse JSON and cache into memory for later
            await Globals.DepartmentService.PrecacheDataAsync();
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