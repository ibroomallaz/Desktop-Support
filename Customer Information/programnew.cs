class ProgramNew
{
    public async static void NewMain()
    {
        try
        {
            //Download departmental data
            await DepartmentList.GetInfo(Globals.g_DepartmentJSONURL);
            //initiate department object to store departmental data
            Department departments = await DepartmentList.ReadJson();
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