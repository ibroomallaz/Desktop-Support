using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Schemas;
using System.Windows;

namespace DSAMVVM.MVVM.Services.Updates
{
    public class VersionCheckerUI(IHttpService http)
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));

        private readonly string _installedVersion = Globals.g_AppVersion;
        private readonly string _versionUrl = Globals.g_VersionJSON;

        private const string StatusKey = "VersionCheck";

        public async Task CheckAsync()
        {
            await CheckAsync(showUpToDatePopup: false);
        }

        public async Task CheckAsync(bool showUpToDatePopup)
        {
            var result = await VersionChecker.CheckVersionAsync(_versionUrl, _http);

            if (!result.Success)
            {
                UiNotify.Error(
                    "Version check error",
                    result.Error ?? "Unknown error",
                    alsoStatusBar: true,
                    key: StatusKey);
                return;
            }

            bool anyUpdateShown = NotifyUser(result.Info!, showUpToDatePopup);
            ReportSuccess();

            if (!anyUpdateShown && showUpToDatePopup)
            {
                MessageBox.Show(
                    "Application is up to date",
                    "Check for Updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ReportSuccess()
        {
            UiNotify.Info(
                $"Version: {_installedVersion}.",
                showStatusBar: true,
                key: StatusKey);
        }

        private bool NotifyUser(VersionInfo versionInfo, bool showUpToDatePopup)
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

            bool shown = false;

            if (isStableUpdate && !isBetaUser && versionInfo.Current != null)
            {
                ShowUpdateNotice(
                    title: "Update Available",
                    newVersion: versionInfo.Current.Version!,
                    location: versionInfo.Current.Location,
                    changelog: versionInfo.Current.Changelog,
                    isBeta: false);
                shown = true;
            }

            if (isBetaUser)
            {
                if (isBetaUpdate)
                {
                    ShowUpdateNotice(
                        title: "PreRelease Update Available",
                        newVersion: versionInfo.PreRelease!.Version!,
                        location: versionInfo.PreRelease.Location,
                        changelog: versionInfo.PreRelease.Changelog,
                        isBeta: true);
                    shown = true;
                }
                else if (!isBetaHigherThanStable && isStableUpdate && versionInfo.Current != null)
                {
                    ShowUpdateNotice(
                        title: "Stable Update Recommended",
                        newVersion: versionInfo.Current.Version!,
                        location: versionInfo.Current.Changelog,
                        changelog: versionInfo.Current.Changelog,
                        isBeta: false);
                    shown = true;
                }
            }

            return shown;
        }

        // Non-blocking status with actionable links
        private void ShowUpdateNotice(string title, string newVersion, string? location, string? changelog, bool isBeta)
        {
            var text = $"{title}: A new version ({newVersion}) is available — you’re on {_installedVersion}.";

            var links = new System.Collections.Generic.List<UiNotify.StatusLink>();

            if (Uri.TryCreate(location ?? Globals.g_SharepointHome, UriKind.Absolute, out var downloadUri))
                links.Add(UiNotify.Link.External("Download", downloadUri, "Get the update"));

            if (!string.IsNullOrWhiteSpace(changelog) && Uri.TryCreate(changelog, UriKind.Absolute, out var notesUri))
                links.Add(UiNotify.Link.External("Release notes", notesUri, "View changes"));

            UiNotify.WarnWithLinks(
                text,
                sticky: false,
                priority: 1,
                key: StatusKey,
                [.. links]);
        }
    }
}
