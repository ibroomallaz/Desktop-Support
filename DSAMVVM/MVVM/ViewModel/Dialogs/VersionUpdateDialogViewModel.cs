using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DSAMVVM.Core.Interfaces;

namespace DSAMVVM.MVVM.ViewModel.Dialogs
{
    // Manages the data bindings and execution logic for the simplified application update dialog
    public sealed class VersionUpdateDialogViewModel : INotifyPropertyChanged
    {
        private readonly IUpdaterService _updaterService;

        private bool _isDownloading;
        private string _statusText = string.Empty;

        public string TitleText { get; } = "Update Available";
        public string HeaderText { get; }
        public string SubText { get; }

        public string? DownloadUrl { get; }
        public string? ChangelogText { get; }
        public string ChangeLogUrl { get; }

        // Evaluates property validity for conditional UI rendering and command execution
        public bool HasDownload => Uri.TryCreate(DownloadUrl ?? "", UriKind.Absolute, out _);
        public bool HasNotesText => !string.IsNullOrWhiteSpace(ChangelogText);
        public bool NotesButtonEnabled => !string.IsNullOrWhiteSpace(ChangeLogUrl) || HasNotesText;

        public Visibility ChangeLogCardVisibility => HasNotesText ? Visibility.Visible : Visibility.Collapsed;

        // Controls the active state of the installation process and triggers UI layout changes
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

        // Holds the current string output from the IUpdaterService
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

        public event Action? RequestClose;
        public event Action? RequestFocusNotes;
        public event PropertyChangedEventHandler? PropertyChanged;

        public VersionUpdateDialogViewModel(
            IUpdaterService updaterService,
            string installedVersion,
            string newVersion,
            bool isPreRelease,
            string? downloadUrl,
            string? changelogText,
            string changeLogUrl)
        {
            _updaterService = updaterService;

            HeaderText = isPreRelease
                ? $"A pre-release build is available: {newVersion}"
                : $"A new version is available: {newVersion}";
            SubText = $"Installed: {installedVersion}";

            DownloadUrl = string.IsNullOrWhiteSpace(downloadUrl) ? null : downloadUrl.Trim();
            ChangelogText = string.IsNullOrWhiteSpace(changelogText) ? null : changelogText.Trim();
            ChangeLogUrl = changeLogUrl?.Trim() ?? string.Empty;

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

        // Executes the background download pipeline and binds progress reports to the StatusText property
        private async void ExecuteInstall()
        {
            if (string.IsNullOrWhiteSpace(DownloadUrl)) return;

            IsDownloading = true;

            var progress = new Progress<string>(message => StatusText = message);
            await _updaterService.DownloadAndInstallAsync(DownloadUrl, progress);

            IsDownloading = false;
        }

        // Executes the system default web browser for a given URL
        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Internal implementation of ICommand for binding actions to the View
        private sealed class RelayCommand(Predicate<object?> can, Action<object?> run) : ICommand
        {
            private readonly Predicate<object?> _can = can;
            private readonly Action<object?> _run = run;

            public bool CanExecute(object? p) => _can(p);
            public void Execute(object? p) => _run(p);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }
    }
}