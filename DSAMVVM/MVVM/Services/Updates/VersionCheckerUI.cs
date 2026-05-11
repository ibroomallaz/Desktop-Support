using System.Windows;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Schemas;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.View.Dialogs;
using DSAMVVM.MVVM.ViewModel.Dialogs;

namespace DSAMVVM.MVVM.Services.Updates
{
    // Fetches once per cycle; enforces required; prompts once per version; implements scheduler handler
    public class VersionCheckerUI(IHttpService http, IUpdaterService updater, AppSettings settings) : IVersionCheckHandler
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly IUpdaterService _updaterService = updater ?? throw new ArgumentNullException(nameof(updater));
        private readonly AppSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        private readonly string _installedVersion = Globals.g_AppVersion;

        // Dynamically evaluates the active update URL based on application configuration
        private string ActiveVersionUrl => _settings.Updates.UseInternalTestingSources
            ? Globals.g_TestVersionJSON
            : Globals.g_VersionJSON;

        private const string StatusKey = "VersionCheck.Status";
        private const string Cat = "Version.UI";
        private const string RequiredCat = "Version.Required";

        // In-memory state tracking to prevent duplicate prompts during a single application session
        private VersionCheckResult? _cached;
        private DateTime _lastFetchUtc;
        private readonly TimeSpan _cacheWindow = TimeSpan.FromMinutes(2);
        private string? _lastPromptedStable;
        private string? _lastPromptedPre;

        // Evaluates application version against required minimums and triggers blocking actions if obsolete
        public async Task EnforceRequiredAsync()
        {
            var url = ActiveVersionUrl;
            Log.Info(RequiredCat, $"enforce.start installed=\"{_installedVersion}\" url=\"{url}\"");
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

            var updatePayload = res.Info?.Current ?? new CurrentVersion
            {
                Location = res.StableLocation,
                Version = res.StableVersion,
                MsiUrl = res.StableMsiUrl,
                SetupUrl = res.StableSetupUrl,
                RequiredDotNetVersion = res.StableRequiredDotNetVersion
            };

            await ShowRequiredBlockingAsync(GetPreferredOwner(), minReq!, msg, updatePayload);
        }

        public async Task CheckAsync() => await CheckAsync(false);

        // Executes a standard version check and triggers the appropriate UI elements based on the result
        public async Task CheckAsync(bool showUpToDatePopup)
        {
            var url = ActiveVersionUrl;
            Log.Info(Cat, $"check.start installed=\"{_installedVersion}\" url=\"{url}\"");

            var res = await FetchAsync();
            if (res == null || (!res.Success && res.Info == null && !res.HasAnyStable && !res.HasAnyPre))
            {
                UiNotify.Error("Version check error", res?.Error ?? "Unknown error", alsoStatusBar: true, key: StatusKey);
                Log.Info(Cat, "check.error");
                return;
            }

            var stablePayload = res.Info?.Current ?? new CurrentVersion
            {
                Version = res.StableVersion,
                Location = res.StableLocation,
                Changelog = res.StableChangelog,
                MsiUrl = res.StableMsiUrl,
                SetupUrl = res.StableSetupUrl,
                RequiredDotNetVersion = res.StableRequiredDotNetVersion
            };

            var prePayload = new CurrentVersion
            {
                Version = res.Info?.PreRelease?.Version ?? res.PreVersion,
                Location = res.Info?.PreRelease?.Location ?? res.PreLocation,
                Changelog = res.Info?.PreRelease?.Changelog ?? res.PreChangelog,
                MsiUrl = res.Info?.PreRelease?.MsiUrl ?? res.PreMsiUrl,
                SetupUrl = res.Info?.PreRelease?.SetupUrl ?? res.PreSetupUrl,
                RequiredDotNetVersion = res.Info?.PreRelease?.RequiredDotNetVersion ?? res.PreRequiredDotNetVersion
            };

            var preExists = res.Info?.PreRelease?.Exists ?? res.PreExists;

            // Validates required bounds before processing optional updates
            if (!string.IsNullOrWhiteSpace(res.RequiredMinVersion) &&
                VersionChecker.IsNewerVersion(_installedVersion, res.RequiredMinVersion))
            {
                var msg = res.RequiredMessage ?? "A newer version is required to continue.";
                await ShowRequiredBlockingAsync(GetPreferredOwner(), res.RequiredMinVersion!, msg, stablePayload);
                return;
            }

            Log.Info(Cat, $"check.info stable=\"{Val(stablePayload.Version)}\" pre=\"{Val(prePayload.Version)}\" pre.exists={(preExists ? "true" : "false")}");

            var showed = ShowPopupIfNewer(stablePayload, preExists, prePayload);

            UiNotify.Info($"Version: {_installedVersion}.", showStatusBar: true, key: StatusKey);
            Log.Debug(Cat, "report.success");

            if (!showed && showUpToDatePopup)
            {
                UiNotify.Info("You’re up to date.", showStatusBar: true, key: StatusKey);

                var owner = GetPreferredOwner();
                var msgBoxText = $"No updates found.  Version: ({_installedVersion}).";

                if (owner != null)
                {
                    MessageBox.Show(owner, msgBoxText, "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(msgBoxText, "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                Log.Info(Cat, "check.up-to-date.shown");
            }
        }

        // Reduces redundant network requests during automated polling cycles
        private async Task<VersionCheckResult?> FetchAsync()
        {
            if (_cached != null && (DateTime.UtcNow - _lastFetchUtc) < _cacheWindow)
            {
                Log.Debug(Cat, "fetch.cache.hit");
                return _cached;
            }

            var url = ActiveVersionUrl;
            var res = await VersionChecker.CheckVersionAsync(url, _http);
            if (res.Success || res.Info != null || res.HasAnyStable || res.HasAnyPre)
            {
                _cached = res;
                _lastFetchUtc = DateTime.UtcNow;
                Log.Debug(Cat, "fetch.cache.store");
            }
            return res;
        }

        // Determines version hierarchy and handles session-level deduplication before triggering UI
        private bool ShowPopupIfNewer(CurrentVersion stable, bool preExists, CurrentVersion pre)
        {
            var newerStable = !string.IsNullOrWhiteSpace(stable.Version) && VersionChecker.IsNewerVersion(_installedVersion, stable.Version!);
            var newerPre = preExists && !string.IsNullOrWhiteSpace(pre.Version) && VersionChecker.IsNewerVersion(_installedVersion, pre.Version!);

            Log.Info(Cat, $"decide newer.stable={(newerStable ? "true" : "false")} newer.pre={(newerPre ? "true" : "false")}");

            var owner = GetPreferredOwner();

            if (newerStable)
            {
                if (string.Equals(_lastPromptedStable, stable.Version, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info(Cat, "popup.stable.skip duplicate");
                    return false;
                }

                ShowUpdateDialog(owner, stable, isPre: false);
                _lastPromptedStable = stable.Version;
                Log.Info(Cat, "popup.stable.shown");
                return true;
            }

            if (newerPre)
            {
                if (string.Equals(_lastPromptedPre, pre.Version, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info(Cat, "popup.pre.skip duplicate");
                    return false;
                }

                ShowUpdateDialog(owner, pre, isPre: true);
                _lastPromptedPre = pre.Version;
                Log.Info(Cat, "popup.pre.shown");
                return true;
            }

            Log.Info(Cat, "popup.none");
            return false;
        }

        // Instantiates the MVVM dialog components on the main dispatcher thread
        private void ShowUpdateDialog(Window? owner, CurrentVersion updateInfo, bool isPre)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var viewModel = new VersionUpdateDialogViewModel(
                    _updaterService,
                    _installedVersion,
                    updateInfo,
                    isPre
                );

                var dialog = new VersionUpdateDialog
                {
                    DataContext = viewModel,
                    Owner = owner ?? GetPreferredOwner()
                };

                viewModel.RequestClose += () => dialog.Close();

                dialog.ShowDialog();
            });
        }

        // Enforces application exit if an update is mandatory, utilizing the automated pipeline if approved
        private async Task ShowRequiredBlockingAsync(Window? owner, string minVersion, string message, CurrentVersion updateInfo)
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

            if (result == MessageBoxResult.OK && updateInfo != null)
            {
                await _updaterService.DownloadAndInstallAsync(updateInfo);
            }
            else
            {
                Application.Current?.Shutdown();
            }
        }

        // Locates the topmost active application window to use as a dialog owner
        private static Window? GetPreferredOwner()
        {
            var active = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            return active ?? Application.Current?.MainWindow;
        }

        private static string Val(string? s) => string.IsNullOrWhiteSpace(s) ? "(none)" : s!;
    }
}