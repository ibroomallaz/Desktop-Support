using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Services.Updates;

namespace DSAMVVM.MVVM.ViewModel
{
    // Exposes application metadata and version management commands to the About view
    public sealed class AboutViewModel : INotifyPropertyChanged
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
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly IHttpService _http;
        private readonly IUpdaterService _updater;
        private bool _busy;

        public AboutViewModel(IHttpService http, IUpdaterService updater)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _updater = updater ?? throw new ArgumentNullException(nameof(updater));

            OpenGitHubCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, "https://github.com/ibroomallaz/Desktop-Support"));
            OpenSharePointCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, Globals.g_SharepointHome));
            CheckVersionCommand = new AsyncCommand(CheckVersionAsync, () => !_busy);

            // Flips the boolean and notifies the UI to refresh the AppVersion string
            ToggleVersionCommand = new RelayCommand(_ =>
            {
                _showExtendedVersion = !_showExtendedVersion;
                OnPropertyChanged(nameof(AppVersion));
            });
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Executes a manual version check pipeline and manages UI state tracking
        private async Task CheckVersionAsync()
        {
            const string key = "VersionCheck";
            _busy = true;
            (CheckVersionCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            try
            {
                UiNotify.Progress(UiNotify.ProgressOf(key), "Checking for updates…");

                var checker = new VersionCheckerUI(_http, _updater);
                await checker.CheckAsync(showUpToDatePopup: true);
            }
            finally
            {
                UiNotify.RemoveKey(UiNotify.ProgressOf(key));
                _busy = false;
                (CheckVersionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            }
        }

        // Internal implementation of ICommand for binding synchronous actions
        private sealed class RelayCommand(Action<object?> exec, Func<bool>? can = null) : ICommand
        {
            private readonly Action<object?> _exec = exec;
            private readonly Func<bool>? _can = can;

            public bool CanExecute(object? p) => _can?.Invoke() ?? true;
            public void Execute(object? p) => _exec(p);
            public event EventHandler? CanExecuteChanged;
            public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        // Internal implementation of ICommand for binding asynchronous tasks
        private sealed class AsyncCommand(Func<Task> exec, Func<bool>? can = null) : ICommand
        {
            private readonly Func<Task> _exec = exec;
            private readonly Func<bool>? _can = can;

            public bool CanExecute(object? p) => _can?.Invoke() ?? true;
            public async void Execute(object? p) => await _exec();
            public event EventHandler? CanExecuteChanged;
            public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}