using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Schemas;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel.Dialogs
{
    public sealed class VersionUpdateDialogViewModel : INotifyPropertyChanged
    {
        private readonly IUpdaterService _updaterService;
        private readonly CurrentVersion _updateInfo; // Store the full payload

        private bool _isDownloading;
        private string _statusText = string.Empty;

        public string TitleText { get; } = "Update Available";
        public string HeaderText { get; }
        public string SubText { get; }

        // Mapped from the internal payload
        public string? DownloadUrl => _updateInfo.Location;
        public string? ChangelogText => _updateInfo.Changelog;
        public string ChangeLogUrl { get; } // Kept for legacy if needed, otherwise maps to Location

        public bool HasDownload => !string.IsNullOrWhiteSpace(_updateInfo.MsiUrl) || !string.IsNullOrWhiteSpace(_updateInfo.Location);
        public bool HasNotesText => !string.IsNullOrWhiteSpace(ChangelogText);
        public bool NotesButtonEnabled => !string.IsNullOrWhiteSpace(ChangeLogUrl) || HasNotesText;

        public Visibility ChangeLogCardVisibility => HasNotesText ? Visibility.Visible : Visibility.Collapsed;

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                _isDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotDownloading));
            }
        }

        public bool IsNotDownloading => !IsDownloading;

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public ICommand InstallUpdateCommand { get; }
        public ICommand OpenReleaseNotesCommand { get; }
        public ICommand CloseCommand { get; }

        // Logic to determine if we are about to do an admin-level upgrade
        public bool WillUpgradeRuntime => !Globals.IsTargetRuntimePresent(_updateInfo.RequiredDotNetVersion);

        // Drives the visibility of the warning block in XAML
        public Visibility AdminWarningVisibility => WillUpgradeRuntime ? Visibility.Visible : Visibility.Collapsed;

        // Helpful for a ToolTip or specialized text block
        public static string AdminWarningText => "A .NET Runtime upgrade is required. Administrative privileges will be requested.";

        public event Action? RequestClose;
        public event Action? RequestFocusNotes;
        public event PropertyChangedEventHandler? PropertyChanged;

        public VersionUpdateDialogViewModel(
            IUpdaterService updaterService,
            string installedVersion,
            CurrentVersion updateInfo, // Refactored signature
            bool isPreRelease)
        {
            _updaterService = updaterService;
            _updateInfo = updateInfo;

            HeaderText = isPreRelease
                ? $"A pre-release build is available: {updateInfo.Version}"
                : $"A new version is available: {updateInfo.Version}";
            SubText = $"Installed: {installedVersion}";

            // Map manual link if one exists
            ChangeLogUrl = updateInfo.Location?.Trim() ?? string.Empty;

            InstallUpdateCommand = new RelayCommand(_ => HasDownload, _ => ExecuteInstall());
            OpenReleaseNotesCommand = new RelayCommand(
                _ => NotesButtonEnabled,
                _ =>
                {
                    if (!string.IsNullOrWhiteSpace(ChangeLogUrl))
                        OpenUrl(ChangeLogUrl);
                    else if (HasNotesText)
                        RequestFocusNotes?.Invoke();
                });
            CloseCommand = new RelayCommand(_ => true, _ => RequestClose?.Invoke());
        }

        private async void ExecuteInstall()
        {
            //passing the full object
            IsDownloading = true;

            var progress = new Progress<string>(message => StatusText = message);

            try
            {
                await _updaterService.DownloadAndInstallAsync(_updateInfo, progress);
            }
            catch (Exception ex)
            {
                StatusText = "Installation failed.";
                Log.Error("UpdateVM", "Update failed", ex);
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class RelayCommand(Predicate<object?> can, Action<object?> run) : ICommand
        {
            private readonly Predicate<object?> _can = can;
            private readonly Action<object?> _run = run;

            public bool CanExecute(object? p) => _can(p);
            public void Execute(object? p) => _run(p);
            public event EventHandler? CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }
        }
    }
}