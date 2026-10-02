using DSAMVVM.MVVM.Model.Schemas;

namespace DSAMVVM.Core.Interfaces.Updates
{
    public interface IUpdaterService
    {
        // Downloads the update from the specified URL to a temporary location and executes it
        // progressReporter: Optional reporter to push status messages back to the UI
        Task DownloadAndInstallAsync(CurrentVersion updateInfo, IProgress<string>? progressReporter = null);
        Task CleanupOldUpdatesAsync();
    }
}