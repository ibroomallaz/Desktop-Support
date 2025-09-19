using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel.Dialogs
{
    public sealed class VersionUpdateDialogViewModel
    {
        // Header
        public string TitleText { get; } = "Update Available";
        public string HeaderText { get; }
        public string SubText { get; }

        // Content
        public string? DownloadUrl { get; }
        public string? ChangelogText { get; }
        public string ChangeLogUrl { get; } // global release-notes page

        // View helpers
        public bool HasDownload => Uri.TryCreate(DownloadUrl ?? "", UriKind.Absolute, out _);
        public bool HasNotesText => !string.IsNullOrWhiteSpace(ChangelogText);
        public bool NotesButtonEnabled => !string.IsNullOrWhiteSpace(ChangeLogUrl) || HasNotesText;

        public Visibility ChangeLogCardVisibility => HasNotesText ? Visibility.Visible : Visibility.Collapsed;
        public Visibility LinksCardVisibility => (HasDownload || !string.IsNullOrWhiteSpace(ChangeLogUrl)) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DownloadRowVisibility => HasDownload ? Visibility.Visible : Visibility.Collapsed;
        public Visibility NotesLinkVisibility => !string.IsNullOrWhiteSpace(ChangeLogUrl) ? Visibility.Visible : Visibility.Collapsed;

        // Commands
        public ICommand DownloadCommand { get; }
        public ICommand OpenReleaseNotesCommand { get; }
        public ICommand CloseCommand { get; }

        // View requests
        public event Action? RequestClose;
        public event Action? RequestFocusNotes;

        public VersionUpdateDialogViewModel(
            string installedVersion,
            string newVersion,
            bool isPreRelease,
            string? downloadUrl,
            string? changelogText,
            string changeLogUrl)
        {
            HeaderText = isPreRelease
                ? $"A pre-release build is available: {newVersion}"
                : $"A new version is available: {newVersion}";
            SubText = $"Installed: {installedVersion}";

            DownloadUrl = string.IsNullOrWhiteSpace(downloadUrl) ? null : downloadUrl.Trim();
            ChangelogText = string.IsNullOrWhiteSpace(changelogText) ? null : changelogText.Trim();
            ChangeLogUrl = changeLogUrl?.Trim() ?? string.Empty;

            DownloadCommand = new RelayCommand(_ => HasDownload, _ => OpenUrl(DownloadUrl));
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

        // Simple launcher kept local for the minimal split
        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        // Minimal command helper
        private sealed class RelayCommand : ICommand
        {
            private readonly Predicate<object?> _can; private readonly Action<object?> _run;
            public RelayCommand(Predicate<object?> can, Action<object?> run) { _can = can; _run = run; }
            public bool CanExecute(object? p) => _can(p);
            public void Execute(object? p) => _run(p);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }
    }
}
