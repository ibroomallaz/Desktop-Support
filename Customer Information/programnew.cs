class ProgramNew
{
    public async static void NewMain()
    {
        //Download departmental data
        await DepartmentList.GetInfo(Globals.g_DepartmentJSONURL);
        //initiate department object to store departmental data
        Department departments = await DepartmentList.ReadJson();
        //Download JSON for quicklink data
        QuickLinks.GetJson();
        //Run Version check and initial processes
        Version.VersionCheck();
        Menus.MainMenu();
    }
}