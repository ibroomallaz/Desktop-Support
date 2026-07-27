namespace DSAMVVM.Core.Interfaces
{
    public interface IApplicationStateService
    {
        string CurrentView { get; set; }
        string RecentQuery { get; set; }
        string RecentSupportTeam { get; set; }
        string RecentError { get; set; }
        string RecentDepartment { get; set; }
    }
}