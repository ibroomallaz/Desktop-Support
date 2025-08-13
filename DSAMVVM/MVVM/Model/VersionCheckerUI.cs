using DSAMVVM.Core;
using DSAMVVM.Core.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace DSAMVVM.MVVM.Model
{
    public class VersionCheckerUI
    {
        private readonly IStatusReporter _status;
        private readonly IHttpService _http;

        private readonly string _installedVersion = Globals.g_AppVersion;
        private readonly string _versionUrl = Globals.g_versionJSON;

        public VersionCheckerUI(IStatusReporter status, IHttpService http)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _ = CheckAsync();
        }

        public async Task CheckAsync()
        {
            var result = await VersionChecker.CheckVersionAsync(_versionUrl, _http);

            if (!result.Success)
            {
                _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                    $"Version check error: {result.Error}. {{0}}",
                    [StatusMessageFactory.ActionLink("Retry", () => _ = CheckAsync())],
                    priority: 3,
                    sticky: true,
                    key: "VersionCheck"
                ));
                return;
            }

            NotifyUser(result.Info!);
            ReportSuccess();
        }

        private void ReportSuccess()
        {
            _status.Report(StatusMessageFactory.Plain(
                $"Version: {_installedVersion}.",
                priority: 0,
                sticky: false,
                key: "VersionCheck"));
        }

        private void NotifyUser(VersionInfo versionInfo)
        {
            bool isBetaUser = _installedVersion.Contains("beta", StringComparison.OrdinalIgnoreCase)
                           || _installedVersion.Contains("alpha", StringComparison.OrdinalIgnoreCase);

            bool isStableUpdate = versionInfo.Current?.Version != null &&
                                  VersionChecker.IsNewerVersion(_installedVersion, versionInfo.Current.Version);

            bool isBetaUpdate = versionInfo.PreRelease?.Exists == true &&
                                !string.IsNullOrWhiteSpace(versionInfo.PreRelease.Version) &&
                                VersionChecker.IsNewerVersion(_installedVersion, versionInfo.PreRelease.Version);

            bool isBetaHigherThanStable = versionInfo.Current?.Version != null &&
                                          versionInfo.PreRelease?.Version != null &&
                                          VersionChecker.IsNewerVersion(versionInfo.Current.Version, versionInfo.PreRelease.Version);

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

            Application.Current.Dispatcher.Invoke(() =>
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
    }
}
