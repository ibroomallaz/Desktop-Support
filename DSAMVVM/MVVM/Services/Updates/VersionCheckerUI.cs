using System.Windows;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.View.Dialogs;

namespace DSAMVVM.MVVM.Services.Updates
{
    // Fetches once per cycle; enforces required; prompts once per version; implements scheduler handler
    public class VersionCheckerUI(IHttpService http) : IVersionCheckHandler
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly string _installedVersion = Globals.g_AppVersion;
        private readonly string _versionUrl = Globals.g_VersionJSON;

        private const string StatusKey = "VersionCheck.Status";
        private const string Cat = "Version.UI";
        private const string RequiredCat = "Version.Required";

        // -> simple in-memory dedupe
        private VersionCheckResult? _cached;
        private DateTime _lastFetchUtc;
        private readonly TimeSpan _cacheWindow = TimeSpan.FromMinutes(2);
        private string? _lastPromptedStable;
        private string? _lastPromptedPre;

        // -> required gate (uses single fetch)
        public async Task EnforceRequiredAsync()
        {
            Log.Info(RequiredCat, $"enforce.start installed=\"{_installedVersion}\" url=\"{_versionUrl}\"");
            var res = await FetchAsync();

            if (res == null || (!res.Success && res.Info == null && !res.HasAnyStable && !res.HasAnyPre))
            {
                Log.Info(RequiredCat, "enforce.skip no-data");
                return;
            }

            var minReq = res.RequiredMinVersion;
            if (string.IsNullOrWhiteSpace(minReq))
            {
                Log.Info(RequiredCat, "enforce.skip no-required");
                return;
            }

            var mustUpdate = VersionChecker.IsNewerVersion(_installedVersion, minReq!);
            Log.Info(RequiredCat, $"enforce.eval installed=\"{_installedVersion}\" required.min=\"{minReq}\" mustUpdate={(mustUpdate ? "true" : "false")}");

            if (!mustUpdate) return;

            var msg = res.RequiredMessage ?? "A newer version is required to continue.";
            var dl = res.Info?.Current?.Location ?? res.StableLocation;
            ShowRequiredBlocking(GetPreferredOwner(), minReq!, msg, dl);
        }

        // -> non-blocking update path (uses single fetch)
        public async Task CheckAsync() => await CheckAsync(false);

        public async Task CheckAsync(bool showUpToDatePopup)
        {
            Log.Info(Cat, $"check.start installed=\"{_installedVersion}\" url=\"{_versionUrl}\"");

            var res = await FetchAsync();
            if (res == null || (!res.Success && res.Info == null && !res.HasAnyStable && !res.HasAnyPre))
            {
                UiNotify.Error("Version check error", res?.Error ?? "Unknown error", alsoStatusBar: true, key: StatusKey);
                Log.Info(Cat, "check.error");
                return;
            }

            // -> honor required gate using same fetch (no extra network)
            if (!string.IsNullOrWhiteSpace(res.RequiredMinVersion) &&
                VersionChecker.IsNewerVersion(_installedVersion, res.RequiredMinVersion))
            {
                var msg = res.RequiredMessage ?? "A newer version is required to continue.";
                var dl = res.Info?.Current?.Location ?? res.StableLocation;
                ShowRequiredBlocking(GetPreferredOwner(), res.RequiredMinVersion!, msg, dl);
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

            var showed = ShowPopupIfNewer(stableVer, stableLoc, stableChg, preExists, preVer, preLoc, preChg);

            UiNotify.Info($"Version: {_installedVersion}.", showStatusBar: true, key: StatusKey);
            Log.Debug(Cat, "report.success");

            if (!showed && showUpToDatePopup)
            {
                UiNotify.Info("You’re up to date.", showStatusBar: true, key: StatusKey);

                // Anchor the MessageBox to the main window to prevent Windows from culling it
                var owner = GetPreferredOwner();
                if (owner != null)
                {
                    MessageBox.Show(
                        owner,
                        $"No updates found.  Version: ({_installedVersion}).",
                        "Up to Date",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Safe fallback if the window handle is completely unavailable
                    MessageBox.Show(
                        $"No updates found.  Version: ({_installedVersion}).",
                        "Up to Date",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                Log.Info(Cat, "check.up-to-date.shown");
            }
        }

        // -> shared fetch with short cache window
        private async Task<VersionCheckResult?> FetchAsync()
        {
            if (_cached != null && (DateTime.UtcNow - _lastFetchUtc) < _cacheWindow)
            {
                Log.Debug(Cat, "fetch.cache.hit");
                return _cached;
            }

            var res = await VersionChecker.CheckVersionAsync(_versionUrl, _http);
            if (res.Success || res.Info != null || res.HasAnyStable || res.HasAnyPre)
            {
                _cached = res;
                _lastFetchUtc = DateTime.UtcNow;
                Log.Debug(Cat, "fetch.cache.store");
            }
            return res;
        }

        // -> compare, session-level dedupe, show dialog
        private bool ShowPopupIfNewer(string? stableVer, string? stableLoc, string? stableChg,
                                      bool preExists, string? preVer, string? preLoc, string? preChg)
        {
            var newerStable = !string.IsNullOrWhiteSpace(stableVer) && VersionChecker.IsNewerVersion(_installedVersion, stableVer!);
            var newerPre = preExists && !string.IsNullOrWhiteSpace(preVer) && VersionChecker.IsNewerVersion(_installedVersion, preVer!);

            Log.Info(Cat, $"decide newer.stable={(newerStable ? "true" : "false")} newer.pre={(newerPre ? "true" : "false")}");

            var owner = GetPreferredOwner();

            if (newerStable)
            {
                if (string.Equals(_lastPromptedStable, stableVer, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info(Cat, "popup.stable.skip duplicate");
                    return false;
                }

                VersionUpdateDialog.ShowFor(owner, _installedVersion, stableVer!, stableLoc, stableChg, isBeta: false);
                _lastPromptedStable = stableVer;
                Log.Info(Cat, "popup.stable.shown");
                return true;
            }

            if (newerPre)
            {
                if (string.Equals(_lastPromptedPre, preVer, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info(Cat, "popup.pre.skip duplicate");
                    return false;
                }

                VersionUpdateDialog.ShowFor(owner, _installedVersion, preVer!, preLoc, preChg, isBeta: true);
                _lastPromptedPre = preVer;
                Log.Info(Cat, "popup.pre.shown");
                return true;
            }

            Log.Info(Cat, "popup.none");
            return false;
        }

        // -> blocking modal for required
        private static void ShowRequiredBlocking(Window? owner, string minVersion, string message, string? downloadUrl)
        {
            var text =
                "This version of the app is no longer supported.\n\n" +
                $"Minimum required version: {minVersion}\n\n" +
                message;

            var result = MessageBox.Show(owner ?? GetPreferredOwner(),
                                         text,
                                         "Update Required",
                                         MessageBoxButton.OKCancel,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK && !string.IsNullOrWhiteSpace(downloadUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = downloadUrl,
                        UseShellExecute = true
                    });
                }
                catch { }
            }

            Application.Current?.Shutdown();
        }

        // -> owner select
        private static Window? GetPreferredOwner()
        {
            var active = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            return active ?? Application.Current?.MainWindow;
        }

        private static string Val(string? s) => string.IsNullOrWhiteSpace(s) ? "(none)" : s!;
    }
}
