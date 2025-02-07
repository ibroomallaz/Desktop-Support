class ProgramNew
{
    public async static void NewMain()
    {
        try
        {
            //Download departmental data, parse JSON and cache into memory for later
            IDepartmentService departmentService = new DepartmentService();
            await departmentService.PrecacheDataAsync();

            //Download JSON for quicklink data
            await QuickLinks.GetJson();
            //Run Version check and initial processes
            await Version.VersionCheck();
            Menus.MainMenu();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during startup: {ex.Message}");
        }
    }
}