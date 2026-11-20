using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Services.Graph;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config.UI;
using DSAMVVM.MVVM.Model.Entra;
using DSAMVVM.MVVM.Services.Status;
using DSAMVVM.MVVM.ViewModel.Cards;

namespace DSAMVVM.MVVM.ViewModel
{
    public class EntraViewModel : ObservableObject, ISearchableViewModel, IDisposable
    {
        private static readonly string[] SignInScopes =
        [
            "User.Read",
            .. EntraService.RequiredDeviceScopes
        ];

        private const string ViewKey = "EntraView";

        private readonly IAuthenticationService _authService;
        private readonly IEntraService _entraService;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;
        private readonly IDeepLinkRoutingService _linkRouter;
        private readonly StatusBus _statusBus;

        public ObservableCollection<EntraHistoryItemViewModel> History { get; } = [];
        public bool HasHistory => History.Count > 0;

        public enum EntraSearchMode
        {
            Device
        }

        private EntraSearchMode _searchMode = EntraSearchMode.Device;
        public bool IsDeviceMode
        {
            get => _searchMode == EntraSearchMode.Device;
            set { if (value) { _searchMode = EntraSearchMode.Device; OnPropertyChanged(); } }
        }

        private string? _error;
        public string? Error
        {
            get => _error;
            private set { _error = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        private double _effectiveOutputFontSize = UiLimits.DefaultFontSize;
        public double EffectiveOutputFontSize
        {
            get => _effectiveOutputFontSize;
            private set
            {
                if (Math.Abs(_effectiveOutputFontSize - value) > 0.01)
                {
                    _effectiveOutputFontSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Typography));
                }
            }
        }

        public CardTypography Typography => CardTypography.FromBase(EffectiveOutputFontSize);

        public bool IsAuthenticated => _authService.IsAuthenticated;
        public string AuthButtonText => IsAuthenticated ? "Sign out" : "Sign in";
        public string AccountDisplayText => IsAuthenticated && !string.IsNullOrWhiteSpace(_authService.CurrentAccountUpn)
            ? _authService.CurrentAccountUpn!
            : "Not signed in";

        public ICommand AuthToggleCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand RemoveHistoryItemCommand { get; }
        public ICommand IncreaseFontCommand { get; }
        public ICommand DecreaseFontCommand { get; }
        public ICommand ResetFontCommand { get; }

        public EntraViewModel(
            IAuthenticationService authService,
            IEntraService entraService,
            ISettingsService settingsSvc,
            IOutputTextSettingsProvider notifier,
            IDeepLinkRoutingService linkRouter,
            StatusBus statusBus)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _entraService = entraService ?? throw new ArgumentNullException(nameof(entraService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));
            _statusBus = statusBus ?? throw new ArgumentNullException(nameof(statusBus));

            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;

            AuthToggleCommand = new RelayCommand(async _ => await ExecuteAuthToggleAsync());
            ClearCommand = new RelayCommand(_ => ClearLog());
            ClearLogCommand = new RelayCommand(_ => ClearLog());
            CollapseAllCommand = new RelayCommand(_ => CollapseAll());
            ExpandAllCommand = new RelayCommand(_ => ExpandAll());
            RemoveHistoryItemCommand = new RelayCommand(param =>
            {
                if (param is EntraHistoryItemViewModel item)
                {
                    History.Remove(item);
                }
            });
            IncreaseFontCommand = new RelayCommand(_ => AdjustFont(+1));
            DecreaseFontCommand = new RelayCommand(_ => AdjustFont(-1));
            ResetFontCommand = new RelayCommand(_ => ResetFont());

            History.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasHistory));

            RefreshEffectiveFont();
            _notifier.Changed += OnOutputFontSettingsChanged;
        }

        private void OnAuthenticationStateChanged(bool isAuthenticated)
        {
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(AuthButtonText));
            OnPropertyChanged(nameof(AccountDisplayText));
        }

        private void OnOutputFontSettingsChanged(object? sender, EventArgs e) => RefreshEffectiveFont();
        private void RefreshEffectiveFont() => EffectiveOutputFontSize = _notifier.GetFontSize(ViewKey);

        private void AdjustFont(int delta)
        {
            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _ = _settingsSvc.AdjustOutputFontSize(s, preferPerView ? ViewKey : null, delta, preferPerView);
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));

            _notifier.NotifyChanged();
        }

        private void ResetFont()
        {
            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _settingsSvc.ResetOutputFontSize(s, preferPerView ? ViewKey : null, preferPerView, defaultSize: (int)UiLimits.DefaultFontSize);
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));

            _notifier.NotifyChanged();
        }

        // --- Search Flow ---
        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;

            if (target != SearchTarget.Entra || string.IsNullOrWhiteSpace(context.Query))
            {
                Error = "Invalid target or empty query.";
                Log.Warn(ViewKey, $"Search aborted: Invalid target ({target}) or empty query.");
                return;
            }

            try
            {
                IsLoading = true;
                Log.Info(ViewKey, $"Starting Entra Device search for '{context.Query}'");

                var result = await searchService.SearchAsync(context, target) as EntraDeviceSearchResult;

                foreach (var item in History)
                {
                    item.IsExpanded = false;
                }

                var entry = new EntraHistoryItemViewModel(
                    context.Query,
                    result,
                    _entraService,
                    _linkRouter)
                {
                    IsExpanded = true
                };

                if (result is { Exists: true })
                {
                    Log.Info(ViewKey, $"Entra search for '{context.Query}' returned {result.Devices.Count} device(s).");
                }
                else
                {
                    Error = result?.ErrorMessage ?? "Entra device not found.";
                    Log.Warn(ViewKey, $"Entra search completed with no matches for '{context.Query}'. Error: {Error}");
                }

                AddHistoryEntry(entry);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Log.Error(ViewKey, $"Exception during Entra device search for '{context.Query}'", ex);

                foreach (var item in History)
                {
                    item.IsExpanded = false;
                }

                var failEntry = new EntraHistoryItemViewModel(
                    context.Query,
                    new EntraDeviceSearchResult { Query = context.Query, Exists = false, ErrorMessage = ex.Message },
                    _entraService,
                    _linkRouter)
                {
                    IsExpanded = true
                };
                AddHistoryEntry(failEntry);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void AddHistoryEntry(EntraHistoryItemViewModel entry)
        {
            entry.RemoveRequested += item => History.Remove(item);
            History.Add(entry);
            while (History.Count > Globals.g_MaxFeedCap)
            {
                History.RemoveAt(0);
            }
        }

        public void CollapseAll()
        {
            foreach (var item in History)
            {
                item.IsExpanded = false;
            }
        }

        public void ExpandAll()
        {
            foreach (var item in History)
            {
                item.IsExpanded = true;
            }
        }

        public void ClearLog()
        {
            History.Clear();
        }

        private async Task ExecuteAuthToggleAsync()
        {
            if (_authService.IsAuthenticated)
            {
                await ExecuteSignOutAsync();
            }
            else
            {
                await ExecuteSignInAsync();
            }
        }

        private async Task ExecuteSignInAsync()
        {
            Log.Info(ViewKey, "Initiating Entra ID token request.");

            _statusBus.Report(new StatusItemBuilder()
                .Key("EntraAuth")
                .Level(StatusLevel.Info)
                .Text("Initializing Entra ID token request...")
                .Build());

            try
            {
                string token = await _authService.GetGraphAccessTokenAsync(SignInScopes);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    Log.Success(ViewKey, "Entra ID sign-in completed successfully.");
                    OnPropertyChanged(nameof(IsAuthenticated));
                    OnPropertyChanged(nameof(AuthButtonText));
                    OnPropertyChanged(nameof(AccountDisplayText));

                    _statusBus.Report(new StatusItemBuilder()
                        .Key("EntraAuth")
                        .Level(StatusLevel.Success)
                        .Text("Successfully signed in.")
                        .Build());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ViewKey, "Entra ID sign-in exception encountered.", ex);

                _statusBus.Report(new StatusItemBuilder()
                    .Key("EntraAuth")
                    .Level(StatusLevel.Error)
                    .Sticky(true)
                    .Text($"AUTH FAILED: {ex.Message}")
                    .Build());
            }
        }

        private async Task ExecuteSignOutAsync()
        {
            Log.Info(ViewKey, "Initiating Entra ID sign-out request.");

            try
            {
                await _authService.SignOutAsync();
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(AuthButtonText));
                OnPropertyChanged(nameof(AccountDisplayText));

                Log.Success(ViewKey, "Entra ID sign-out completed successfully.");

                _statusBus.Report(new StatusItemBuilder()
                    .Key("EntraAuth")
                    .Level(StatusLevel.Success)
                    .Text("Successfully signed out.")
                    .Build());
            }
            catch (Exception ex)
            {
                Log.Error(ViewKey, "Entra ID sign-out exception encountered.", ex);

                _statusBus.Report(new StatusItemBuilder()
                    .Key("EntraAuth")
                    .Level(StatusLevel.Error)
                    .Sticky(true)
                    .Text($"SIGN OUT FAILED: {ex.Message}")
                    .Build());
            }
        }

        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _authService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
                _notifier.Changed -= OnOutputFontSettingsChanged;
            }
            _disposed = true;
        }
    }
}