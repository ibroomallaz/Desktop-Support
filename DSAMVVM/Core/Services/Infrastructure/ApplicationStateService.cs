namespace DSAMVVM.Core.Services.Infrastructure
{
    public class ApplicationStateService : IApplicationStateService
    {
        public string CurrentView { get; set; } = "Unknown";
        public string RecentQuery { get; set; } = string.Empty;
        public string RecentSupportTeam { get; set; } = string.Empty;
        public string RecentError { get; set; } = string.Empty;
        public string RecentDepartment { get; set; } = string.Empty;
    }
}