using System.Windows;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.View.Dialogs;

namespace DSAMVVM.MVVM.Services.Updates
{
    // Coordinates version retrieval and popup presentation
    public class VersionCheckerUI(IHttpService http)
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly string _installedVersion = Globals.g_AppVersion;
        private readonly string _versionUrl = Globals.g_VersionJSON;

        private const string StatusKey = "VersionCheck.Status";
        private const string Cat = "Version.UI";

        public async Task CheckAsync() => await CheckAsync(showUpToDatePopup: false);

        public async Task CheckAsync(bool showUpToDatePopup)
        {
            Log.Info(Cat, $"check.start installed=\"{_installedVersion}\" url=\"{_versionUrl}\"");

            var res = await VersionChecker.CheckVersionAsync(_versionUrl, _http);
            if (!res.Success && res.Info == null && !res.HasAnyStable && !res.HasAnyPre)
            {
                UiNotify.Error("Version check error", res.Error ?? "Unknown error", alsoStatusBar: true, key: StatusKey);
                Log.Info(Cat, "check.error");
                return;
            }

            var stableVer = res.Info?.Current?.Version ?? res.StableVersion;
            var stableLoc = res.Info?.Current?.Location ?? res.StableLocation;
            var stableChg = res.Info?.Current?.Changelog ?? res.StableChangelog;

            var preExists = res.Info?.PreRelease?.Exists ?? res.PreExists;
            var preVer = res.Info?.PreRelease?.Version ?? res.PreVersion;
            var preLoc = res.Info?.PreRelease?.Location ?? res.PreLocation;
            var preChg = res.Info?.PreRelease?.Changelog ?? res.PreChangelog;

            Log.Info(Cat, $"check.info stable=\"{Val(stableVer)}\" pre=\"{Val(preVer)}\" pre.exists={(preExists ? "true" : "false")}");

            bool showed = ShowPopupIfNewer(stableVer, stableLoc, stableChg, preExists, preVer, preLoc, preChg);

            UiNotify.Info($"Version: {_installedVersion}.", showStatusBar: true, key: StatusKey);
            Log.Debug(Cat, "report.success");

            if (!showed && showUpToDatePopup)
            {
                UiNotify.Info("You’re up to date.", showStatusBar: true, key: StatusKey);
                Log.Info(Cat, "check.up-to-date.shown");
            }
        }

        // Compares and displays the popup
        private bool ShowPopupIfNewer(string? stableVer, string? stableLoc, string? stableChg,
                                      bool preExists, string? preVer, string? preLoc, string? preChg)
        {
            bool newerStable = !string.IsNullOrWhiteSpace(stableVer) && VersionChecker.IsNewerVersion(_installedVersion, stableVer!);
            bool newerPre = preExists && !string.IsNullOrWhiteSpace(preVer) && VersionChecker.IsNewerVersion(_installedVersion, preVer!);

            Log.Info(Cat, $"decide newer.stable={(newerStable ? "true" : "false")} newer.pre={(newerPre ? "true" : "false")}");

            var owner = GetPreferredOwner();

            if (newerStable)
            {
                VersionUpdateDialog.ShowFor(owner, _installedVersion, stableVer!, stableLoc, stableChg, isBeta: false);
                Log.Info(Cat, "popup.stable.shown");
                return true;
            }

            if (newerPre)
            {
                VersionUpdateDialog.ShowFor(owner, _installedVersion, preVer!, preLoc, preChg, isBeta: true);
                Log.Info(Cat, "popup.pre.shown");
                return true;
            }

            Log.Info(Cat, "popup.none");
            return false;
        }

        // Chooses an owner window for the popup
        private static Window? GetPreferredOwner()
        {
            var active = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            return active ?? Application.Current?.MainWindow;
        }

        private static string Val(string? s) => string.IsNullOrWhiteSpace(s) ? "(none)" : s!;
    }
}
