using System.Windows.Input;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Services.Updates;
namespace DSAMVVM.MVVM.ViewModel
{
    public sealed class AboutViewModel
    {
        public string AppVersion { get; }
        public IReadOnlyList<string> Developers { get; } =
        [
            "Isaac Broomall (ibroomall)",
            "JJ Velasquez (jjvelasquez)"
        ];

        public ICommand OpenGitHubCommand { get; }
        public ICommand OpenSharePointCommand { get; }
        public ICommand CheckVersionCommand { get; }

        public event EventHandler<string>? OpenUrlRequested;

        private readonly IHttpService _http;
        private bool _busy;

        public AboutViewModel(IHttpService http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            AppVersion = Globals.g_AppVersion;

            OpenGitHubCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, "https://github.com/ibroomallaz/Desktop-Support"));
            OpenSharePointCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, Globals.g_SharepointHome));
            CheckVersionCommand = new AsyncCommand(CheckVersionAsync, () => !_busy);
        }

        private async Task CheckVersionAsync()
        {
            const string key = "VersionCheck";
            _busy = true; (CheckVersionCommand as AsyncCommand)?.RaiseCanExecuteChanged();

            try
            {
                UiNotify.Progress(UiNotify.ProgressOf(key), "Checking for updates…");

                var checker = new VersionCheckerUI(_http);
                await checker.CheckAsync(showUpToDatePopup: true);
            }
            finally
            {
                UiNotify.RemoveKey(UiNotify.ProgressOf(key));
                _busy = false; (CheckVersionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            }
        }

        private sealed class RelayCommand(Action<object?> exec, Func<bool>? can = null) : ICommand
        {
            private readonly Action<object?> _exec = exec;
            private readonly Func<bool>? _can = can;

            public bool CanExecute(object? p) => _can?.Invoke() ?? true;
            public void Execute(object? p) => _exec(p);
            public event EventHandler? CanExecuteChanged;
            public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

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
