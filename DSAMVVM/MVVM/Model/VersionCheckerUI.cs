using DSAMVVM.Core;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;      
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DSAMVVM.MVVM.Model
{
    public class VersionCheckerUI
    {
        private readonly IHttpService _http;

        private readonly string _installedVersion = Globals.g_AppVersion;
        private readonly string _versionUrl = Globals.g_versionJSON;

        public VersionCheckerUI(IHttpService http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task CheckAsync()
        {
            var result = await VersionChecker.CheckVersionAsync(_versionUrl, _http);

            if (!result.Success)
            {
                OnUI(() => UiNotify.Error(
                    "Version check error",
                    result.Error ?? "Unknown error",
                    alsoStatusBar: true,
                    key: "VersionCheck"));
                return;
            }

            NotifyUser(result.Info!);
            ReportSuccess();
        }

        private void ReportSuccess()
        {
            OnUI(() => UiNotify.Info(
                $"Version: {_installedVersion}.",
                showStatusBar: true,
                key: "VersionCheck"));
        }

        private void NotifyUser(VersionInfo versionInfo)
        {
            bool isBetaUser = _installedVersion.Contains("beta", StringComparison.OrdinalIgnoreCase)
                           || _installedVersion.Contains("alpha", StringComparison.OrdinalIgnoreCase);

            bool isStableUpdate = versionInfo.Current?.Version != null
                               && VersionChecker.IsNewerVersion(_installedVersion, versionInfo.Current.Version);

            bool isBetaUpdate = versionInfo.PreRelease?.Exists == true
                             && !string.IsNullOrWhiteSpace(versionInfo.PreRelease.Version)
                             && VersionChecker.IsNewerVersion(_installedVersion, versionInfo.PreRelease.Version);

            bool isBetaHigherThanStable = versionInfo.Current?.Version != null
                                       && versionInfo.PreRelease?.Version != null
                                       && VersionChecker.IsNewerVersion(versionInfo.Current.Version, versionInfo.PreRelease.Version);

            if (isStableUpdate && !isBetaUser && versionInfo.Current != null)
            {
                PromptUpdate("Update Available", versionInfo.Current.Version!, versionInfo.Current.Location, versionInfo.Current.Changelog, isBeta: false);
            }

            if (isBetaUser)
            {
                if (isBetaUpdate)
                {
                    PromptUpdate("PreRelease Update Available", versionInfo.PreRelease!.Version!, versionInfo.PreRelease.Location, versionInfo.PreRelease.Changelog, isBeta: true);
                }
                else if (!isBetaHigherThanStable && isStableUpdate && versionInfo.Current != null)
                {
                    PromptUpdate("Stable Update Recommended", versionInfo.Current.Version!, versionInfo.Current.Location, versionInfo.Current.Changelog, isBeta: false);
                }
            }
        }

        private void PromptUpdate(string title, string newVersion, string? location, string? changelog, bool isBeta)
        {
            location ??= Globals.g_sharepointHome;
            changelog ??= "No details provided.";

            OnUI(() =>
            {
                var result = MessageBox.Show(
                    $"{title}\n\nA new version ({newVersion}) is available.\n\nCurrent version: {_installedVersion}\n\nChanges:\n{changelog}\n\nWould you like to update?",
                    title,
                    MessageBoxButton.OKCancel,
                    isBeta ? MessageBoxImage.Information : MessageBoxImage.Warning);

                if (result == MessageBoxResult.OK)
                {
                    _http.TryOpenUrl(location, out _);
                }
            });
        }
        // Ensures the provided action runs on the application's UI thread.
        // Falls back to immediate execution if no WPF dispatcher is available (e.g., in tests or console apps).
        private static void OnUI(Action action)
        {
            var d = Application.Current?.Dispatcher;
            if (d is null) { action(); return; }
            if (d.CheckAccess()) action();
            else d.BeginInvoke(action, DispatcherPriority.Normal);
        }
    }
}