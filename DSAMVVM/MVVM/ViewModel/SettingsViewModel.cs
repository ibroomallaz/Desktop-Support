using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;

namespace DSAMVVM.MVVM.ViewModel
{
    public class SettingsViewModel : ObeservableObject
    {
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider? _notifier;
        private readonly ISearchService? _searchSvc;

        private AppSettings _settings;

        // --- State Flags ---
        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => Set(ref _hasUnsavedChanges, value);
        }

        private string _statusMessage = "Settings are up to date.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        // --- Commands ---
        public ICommand ApplyCommand { get; }
        public ICommand OpenLogsCommand { get; }
        public ICommand BrowseDeptCommand { get; }
        public ICommand BrowseLinksCommand { get; }
        public ICommand DiscardCommand { get; }
        public ICommand ResetDefaultsCommand { get; }

        // --- Collections ---
        public IReadOnlyList<AppLogLevel> LogLevels { get; } =
            [AppLogLevel.Off, AppLogLevel.Debug, AppLogLevel.Info, AppLogLevel.Warn, AppLogLevel.Error];

        public IReadOnlyList<int> RetentionOptions { get; } =
            [7, 14, 30, 90, 180, 365, -1];

        public List<int> HistorySizeOptions { get; } = [0, 5, 10, 15, 20, 25];

        public IReadOnlyList<double> InitialFontSizeOptions { get; } =
            [10d, 12d, 14d, 16d, 18d, 20d, 22d];

        public ObservableCollection<string> DataSourceOptions { get; } = ["Web", "File"];

        // --- Exposed Sub-Settings ---
        // These point directly into the _settings object
        public LinksUiSettings LinksSettings => _settings.Ui.Links;
        public SearchSettings SearchSettings => _settings.Ui.Search;
        public TrayUiSettings TraySettings => _settings.Ui.Tray;

        // --- UI State Properties ---
        private double _defaultFontSize;
        public double DefaultFontSize
        {
            get => _defaultFontSize;
            set
            {
                var v = UiLimits.ClampFontSize(value);
                if (Set(ref _defaultFontSize, v))
                {
                    if (!UsePerViewOverride) SyncPerViewToDefault();
                    SetModified();
                }
            }
        }

        private bool _usePerViewOverride;
        public bool UsePerViewOverride
        {
            get => _usePerViewOverride;
            set
            {
                if (Set(ref _usePerViewOverride, value))
                {
                    if (!value) SyncPerViewToDefault();
                    SetModified();
                }
            }
        }

        private double _userFontSize, _computerFontSize, _groupFontSize, _entraFontSize;
        public double UserFontSize { get => _userFontSize; set { if (Set(ref _userFontSize, UiLimits.ClampFontSize(value))) SetModified(); } }
        public double ComputerFontSize { get => _computerFontSize; set { if (Set(ref _computerFontSize, UiLimits.ClampFontSize(value))) SetModified(); } }
        public double GroupFontSize { get => _groupFontSize; set { if (Set(ref _groupFontSize, UiLimits.ClampFontSize(value))) SetModified(); } }
        public double EntraFontSize { get => _entraFontSize; set { if (Set(ref _entraFontSize, UiLimits.ClampFontSize(value))) SetModified(); } }

        private AppLogLevel _minimumLogLevel;
        public AppLogLevel MinimumLogLevel { get => _minimumLogLevel; set { if (Set(ref _minimumLogLevel, value)) SetModified(); } }

        private int _retentionDays;
        public int RetentionDays { get => _retentionDays; set { if (Set(ref _retentionDays, value)) SetModified(); } }

        private bool _useSavedSearchHistory;
        public bool UseSavedSearchHistory { get => _useSavedSearchHistory; set { if (Set(ref _useSavedSearchHistory, value)) SetModified(); } }

        private int _maxSearchHistory;
        public int MaxSearchHistory { get => _maxSearchHistory; set { if (Set(ref _maxSearchHistory, Math.Max(0, value))) SetModified(); } }

        private bool _useCustomDept;
        public bool UseCustomDept { get => _useCustomDept; set { if (Set(ref _useCustomDept, value)) SetModified(); } }

        private string _deptSource = "Web";
        public string DeptSource { get => _deptSource; set { if (Set(ref _deptSource, value)) SetModified(); } }

        private string _deptUri = string.Empty;
        public string DeptUri { get => _deptUri; set { if (Set(ref _deptUri, value)) SetModified(); } }

        private bool _useCustomLinks;
        public bool UseCustomLinks { get => _useCustomLinks; set { if (Set(ref _useCustomLinks, value)) SetModified(); } }

        private string _linksSource = "Web";
        public string LinksSource { get => _linksSource; set { if (Set(ref _linksSource, value)) SetModified(); } }

        private string _linksUri = string.Empty;
        public string LinksUri { get => _linksUri; set { if (Set(ref _linksUri, value)) SetModified(); } }

        private bool _enableTrayIcon;
        public bool EnableTrayIcon
        {
            get => _enableTrayIcon;
            set
            {
                if (Set(ref _enableTrayIcon, value))
                {
                    if (!value) { MinimizeToTray = false; CloseToTray = false; }
                    SetModified();
                }
            }
        }

        private bool _minimizeToTray;
        public bool MinimizeToTray { get => _minimizeToTray; set { if (Set(ref _minimizeToTray, value)) SetModified(); } }

        private bool _closeToTray;
        public bool CloseToTray { get => _closeToTray; set { if (Set(ref _closeToTray, value)) SetModified(); } }

        private bool _enablePreReleaseChannel;
        public bool EnablePreReleaseChannel { get => _enablePreReleaseChannel; set { if (Set(ref _enablePreReleaseChannel, value)) SetModified(); } }

        // --- Constructors ---
        public SettingsViewModel()
            : this(App.Services.GetRequiredService<ISettingsService>(),
                   App.Services.GetService<IOutputTextSettingsProvider>(),
                   App.Services.GetService<ISearchService>(),
                   App.Services.GetService<IDepartmentService>(),
                   App.Services.GetService<ILinksService>())
        { }

        public SettingsViewModel(
            ISettingsService settingsSvc,
            IOutputTextSettingsProvider? notifier,
            ISearchService? searchSvc,
            IDepartmentService? deptService = null,
            ILinksService? linksService = null)
        {
            _settingsSvc = settingsSvc;
            _notifier = notifier;
            _searchSvc = searchSvc;

            _settings = App.Settings ?? new AppSettings();

            LoadValuesFromSettings();

            ApplyCommand = new RelayCommand(_ => Apply(deptService, linksService));
            DiscardCommand = new RelayCommand(_ => LoadValuesFromSettings(), _ => HasUnsavedChanges);
            ResetDefaultsCommand = new RelayCommand(_ => ResetToFactoryDefaults());

            OpenLogsCommand = new RelayCommand(_ => OpenLogsFolder());
            BrowseDeptCommand = new RelayCommand(_ => BrowseForFile(path => DeptUri = path));
            BrowseLinksCommand = new RelayCommand(_ => BrowseForFile(path => LinksUri = path));
        }

        // --- Logic ---

        private void LoadValuesFromSettings()
        {
            _settings.ApplyDefaultsAndClamp();

            DefaultFontSize = _settings.Ui.Font.DefaultSize;
            UsePerViewOverride = _settings.Ui.Font.ViewFontSizeOverride;
            UserFontSize = GetViewSize("UserView", DefaultFontSize);
            ComputerFontSize = GetViewSize("ComputerView", DefaultFontSize);
            GroupFontSize = GetViewSize("GroupView", DefaultFontSize);
            EntraFontSize = GetViewSize("EntraView", DefaultFontSize);

            MinimumLogLevel = _settings.Logging.MinimumLevel;
            RetentionDays = _settings.Logging.RetentionDays;

            UseSavedSearchHistory = _settings.Ui.Search.UseSavedSearchHistory;
            EnsureHistoryOption(_settings.Ui.Search.MaxSearchHistory);
            MaxSearchHistory = _settings.Ui.Search.MaxSearchHistory;

            UseCustomDept = _settings.Paths.DepartmentData.UseCustomSource;
            DeptSource = MatchSourceOption(_settings.Paths.DepartmentData.Source);
            DeptUri = _settings.Paths.DepartmentData.Uri;

            UseCustomLinks = _settings.Paths.LinksData.UseCustomSource;
            LinksSource = MatchSourceOption(_settings.Paths.LinksData.Source);
            LinksUri = _settings.Paths.LinksData.Uri;

            EnableTrayIcon = _settings.Ui.Tray.EnableTrayIcon;
            MinimizeToTray = _settings.Ui.Tray.MinimizeToTray;
            CloseToTray = _settings.Ui.Tray.CloseToTray;
            EnablePreReleaseChannel = _settings.Updates.EnablePreReleaseChannel;

            HasUnsavedChanges = false;
            StatusMessage = "Settings are up to date.";
        }

        private void ResetToFactoryDefaults()
        {
            var res = System.Windows.MessageBox.Show(
                "This will reset all preferences and data paths to factory defaults. Proceed?",
                "Reset to Defaults", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (res != System.Windows.MessageBoxResult.Yes) return;

            // 1. Replace reference with fresh defaults
            _settings = new AppSettings();

            // 2. Refresh UI bindings for sub-objects (LinksSettings, etc.)
            OnPropertyChanged(nameof(LinksSettings));
            OnPropertyChanged(nameof(SearchSettings));
            OnPropertyChanged(nameof(TraySettings));

            // 3. Load values into VM properties
            LoadValuesFromSettings();

            // 4. Set state to modified so Apply saves this new object
            HasUnsavedChanges = true;
            StatusMessage = "Factory defaults loaded. Click Apply to save.";
            UiNotify.Info("Defaults loaded. Apply to confirm.", showStatusBar: true);
        }

        private void SetModified()
        {
            if (!HasUnsavedChanges) HasUnsavedChanges = true;
        }

        private async void Apply(IDepartmentService? deptService, ILinksService? linksService)
        {
            try
            {
                // 1. Detect changes by comparing UI properties against the current active settings.

                var active = App.Settings;

                bool deptChanged = active.Paths.DepartmentData.UseCustomSource != UseCustomDept ||
                                   !string.Equals(active.Paths.DepartmentData.Source, DeptSource, StringComparison.OrdinalIgnoreCase) ||
                                   !string.Equals(active.Paths.DepartmentData.Uri, DeptUri);

                bool linksChanged = active.Paths.LinksData.UseCustomSource != UseCustomLinks ||
                                    !string.Equals(active.Paths.LinksData.Source, LinksSource, StringComparison.OrdinalIgnoreCase) ||
                                    !string.Equals(active.Paths.LinksData.Uri, LinksUri);

                bool trayChanged = active.Ui.Tray.EnableTrayIcon != EnableTrayIcon;

                // 2. Sync UI Properties -> Local _settings object
                _settings.Ui.Font.DefaultSize = DefaultFontSize;
                _settings.Ui.Font.ViewFontSizeOverride = UsePerViewOverride;
                UpsertViewSize("UserView", UserFontSize);
                UpsertViewSize("ComputerView", ComputerFontSize);
                UpsertViewSize("GroupView", GroupFontSize);
                UpsertViewSize("EntraView", EntraFontSize);

                _settings.Logging.MinimumLevel = MinimumLogLevel;
                _settings.Logging.RetentionDays = RetentionDays == -1 ? 36500 : Math.Max(1, RetentionDays);

                _settings.Ui.Search.UseSavedSearchHistory = UseSavedSearchHistory;
                _settings.Ui.Search.MaxSearchHistory = MaxSearchHistory;

                _settings.Paths.DepartmentData.UseCustomSource = UseCustomDept;
                _settings.Paths.DepartmentData.Source = DeptSource;
                _settings.Paths.DepartmentData.Uri = DeptUri;

                _settings.Paths.LinksData.UseCustomSource = UseCustomLinks;
                _settings.Paths.LinksData.Source = LinksSource;
                _settings.Paths.LinksData.Uri = LinksUri;

                _settings.Ui.Tray.EnableTrayIcon = EnableTrayIcon;
                _settings.Ui.Tray.MinimizeToTray = MinimizeToTray;
                _settings.Ui.Tray.CloseToTray = CloseToTray;
                _settings.Updates.EnablePreReleaseChannel = EnablePreReleaseChannel;

                _settings.ApplyDefaultsAndClamp();

                // 3. Transfer the data to the global reference.
                active.Ui = _settings.Ui;
                active.Paths = _settings.Paths;
                active.Logging = _settings.Logging;
                active.Updates = _settings.Updates;
                active.Meta = _settings.Meta;

                // 4. Save the global object to disk
                var path = Path.Combine(Globals.g_AppDir, "settings.json");
                _settingsSvc.RequestSave(active, path);
                TryFlushPendingSaves(_settingsSvc);

                // 5. Update UI state
                HasUnsavedChanges = false;
                StatusMessage = "Settings successfully applied!";
                UiNotify.Info("Settings applied.", showStatusBar: true);

                // 6. Trigger Side Effects
                Log.ApplySettings(active);
                _searchSvc?.ConfigureHistory(active.Ui.Search.UseSavedSearchHistory, active.Ui.Search.MaxSearchHistory);
                if (!active.Ui.Search.UseSavedSearchHistory) _searchSvc?.ClearHistory();

                if (deptChanged) _ = deptService?.ReloadDataAsync();
                if (linksChanged) _ = linksService?.ReloadLinksDataAsync();

                if (trayChanged)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (System.Windows.Application.Current is App myApp) myApp.ToggleTrayIcon(EnableTrayIcon);
                    });
                }

                _notifier?.NotifyChanged();

                // 7. Success Message Timer
                await Task.Delay(3000);
                if (!HasUnsavedChanges)
                {
                    StatusMessage = "Settings are up to date.";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = "Error saving settings.";
                UiNotify.Error("Settings Error", ex.Message, alsoStatusBar: true);
                Log.Error("Settings", "Failed to apply settings", ex);
            }
        }

        private void OpenLogsFolder()
        {
            var dir = ResolveLogDirCompat(_settingsSvc, _settings);
            try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); } catch { }
        }

        private static void BrowseForFile(Action<string> onPathSelected)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON Data (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Select Data File",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true) onPathSelected(dlg.FileName);
        }

        // --- Helpers ---
        private string MatchSourceOption(string input) =>
            DataSourceOptions.FirstOrDefault(x => x.Equals(input, StringComparison.OrdinalIgnoreCase)) ?? "Web";

        private void SyncPerViewToDefault()
        {
            UserFontSize = DefaultFontSize;
            ComputerFontSize = DefaultFontSize;
            GroupFontSize = DefaultFontSize;
            EntraFontSize = DefaultFontSize;
        }

        private double GetViewSize(string key, double fallback)
        {
            if (_settings.Ui.ViewFontSizes.TryGetValue(key, out var entry) && entry != null && entry.FontSize > 0)
                return UiLimits.ClampFontSize(entry.FontSize);
            return UiLimits.ClampFontSize(fallback);
        }

        private void UpsertViewSize(string key, double size)
        {
            if (!_settings.Ui.ViewFontSizes.TryGetValue(key, out var entry) || entry == null)
                _settings.Ui.ViewFontSizes[key] = entry = new ViewFontSetting();

            if (entry.FontSize != size)
            {
                entry.FontSize = UiLimits.ClampFontSize(size);
                SetModified();
            }
        }

        private void EnsureHistoryOption(int value)
        {
            if (!HistorySizeOptions.Contains(value))
            {
                HistorySizeOptions.Add(value);
                HistorySizeOptions.Sort();
                OnPropertyChanged(nameof(HistorySizeOptions));
            }
        }

        private static void TryFlushPendingSaves(ISettingsService svc)
        {
            try
            {
                var methodInfo = svc.GetType().GetMethod("FlushPendingSaves", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                methodInfo?.Invoke(svc, null);
            }
            catch { }
        }

        private static string ResolveLogDirCompat(ISettingsService svc, AppSettings s)
        {
            try
            {
                var methodInfo = svc.GetType().GetMethod("ResolveLogDir", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (methodInfo != null)
                {
                    var val = methodInfo.Invoke(svc, [s]) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val!;
                }
            }
            catch { }
            Directory.CreateDirectory(Globals.g_LogsDir);
            return Globals.g_LogsDir;
        }
    }
}