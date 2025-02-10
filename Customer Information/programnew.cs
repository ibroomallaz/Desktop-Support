class Program
{
    public async static void Main()
    {
        try
        {
            //Download departmental data, parse JSON and cache into memory for later
            IDepartmentService departmentService = new DepartmentService();
            await departmentService.PrecacheDataAsync();

            //Download JSON for quicklink data
            await QuickLinks.GetJson();
            //Run Version check and initial processes
            await VersionChecker.VersionCheck();
            Menus.MainMenu();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during startup: {ex.Message}");
        }
    }
}