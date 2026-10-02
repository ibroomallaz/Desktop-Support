using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Services.Updates;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    // Exposes application metadata and version management commands to the About view
    public sealed class AboutViewModel : ObservableObject
    {
        private bool _showExtendedVersion;

        // Dynamically outputs either the base semantic version or the extended file version
        public string AppVersion => _showExtendedVersion
            ? $"{Globals.g_AppVersion} ({Globals.g_FileVersion})"
            : Globals.g_AppVersion;

        public IReadOnlyList<string> Developers { get; } =
        [
            "Isaac Broomall (ibroomall)",
            "JJ Velasquez (jjvelasquez)"
        ];

        public ICommand OpenGitHubCommand { get; }
        public ICommand OpenSharePointCommand { get; }
        public ICommand CheckVersionCommand { get; }
        public ICommand ToggleVersionCommand { get; }

        public event EventHandler<string>? OpenUrlRequested;

        private readonly IHttpService _http;
        private readonly IUpdaterService _updater;
        private bool _busy;

        public AboutViewModel(IHttpService http, IUpdaterService updater)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _updater = updater ?? throw new ArgumentNullException(nameof(updater));

            OpenGitHubCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, "https://github.com/ibroomallaz/Desktop-Support"));
            OpenSharePointCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, Globals.g_SharepointHome));
            CheckVersionCommand = new RelayCommand(_ => ExecuteCheckVersion(), _ => !_busy);

            // Flips the boolean and notifies the UI to refresh the AppVersion string
            ToggleVersionCommand = new RelayCommand(_ =>
            {
                _showExtendedVersion = !_showExtendedVersion;
                OnPropertyChanged(nameof(AppVersion));
            });
        }

        private void ExecuteCheckVersion()
        {
            _ = CheckVersionAsync();
        }

        // Executes a manual version check pipeline and manages UI state tracking
        private async Task CheckVersionAsync()
        {
            if (_busy) return;
            const string key = "VersionCheck";
            _busy = true;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                UiNotify.Progress(UiNotify.ProgressOf(key), "Checking for updates…");

                var appSettings = App.Services.GetRequiredService<AppSettings>();
                var checker = new VersionCheckerUI(_http, _updater, appSettings);
                await checker.CheckAsync(showUpToDatePopup: true);
            }
            catch (Exception ex)
            {
                UiNotify.Error("Version Check", $"Check for updates failed: {ex.Message}", ex);
            }
            finally
            {
                UiNotify.RemoveKey(UiNotify.ProgressOf(key));
                _busy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
