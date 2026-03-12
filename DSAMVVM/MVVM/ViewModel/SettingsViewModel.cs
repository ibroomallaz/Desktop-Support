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

        private readonly AppSettings _settings;

        // --- Commands ---
        public ICommand ApplyCommand { get; }
        public ICommand OpenLogsCommand { get; }
        public ICommand BrowseDeptCommand { get; }
        public ICommand BrowseLinksCommand { get; }

        // --- Collections ---
        public IReadOnlyList<AppLogLevel> LogLevels { get; } =
            [AppLogLevel.Off, AppLogLevel.Debug, AppLogLevel.Info, AppLogLevel.Warn, AppLogLevel.Error];

        public IReadOnlyList<int> RetentionOptions { get; } =
            [7, 14, 30, 90, 180, 365, -1];

        public List<int> HistorySizeOptions { get; } = [0, 5, 10, 15, 20, 25];

        public IReadOnlyList<double> InitialFontSizeOptions { get; } =
            [10d, 12d, 14d, 16d, 18d, 20d, 22d];

        // Capitalized display options
        public ObservableCollection<string> DataSourceOptions { get; } = ["Web", "File"];

        // --- Exposed Sub-Settings ---
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
                if (Set(ref _defaultFontSize, v) && !UsePerViewOverride)
                    SyncPerViewToDefault();
            }
        }

        private bool _usePerViewOverride;
        public bool UsePerViewOverride
        {
            get => _usePerViewOverride;
            set
            {
                if (Set(ref _usePerViewOverride, value) && !value)
                    SyncPerViewToDefault();
            }
        }

        private double _userFontSize, _computerFontSize, _groupFontSize, _entraFontSize;
        public double UserFontSize { get => _userFontSize; set => Set(ref _userFontSize, UiLimits.ClampFontSize(value)); }
        public double ComputerFontSize { get => _computerFontSize; set => Set(ref _computerFontSize, UiLimits.ClampFontSize(value)); }
        public double GroupFontSize { get => _groupFontSize; set => Set(ref _groupFontSize, UiLimits.ClampFontSize(value)); }
        public double EntraFontSize { get => _entraFontSize; set => Set(ref _entraFontSize, UiLimits.ClampFontSize(value)); }

        private AppLogLevel _minimumLogLevel;
        public AppLogLevel MinimumLogLevel { get => _minimumLogLevel; set => Set(ref _minimumLogLevel, value); }

        private int _retentionDays;
        public int RetentionDays { get => _retentionDays; set => Set(ref _retentionDays, value); }

        private bool _useSavedSearchHistory;
        public bool UseSavedSearchHistory { get => _useSavedSearchHistory; set => Set(ref _useSavedSearchHistory, value); }

        private int _maxSearchHistory;
        public int MaxSearchHistory { get => _maxSearchHistory; set => Set(ref _maxSearchHistory, Math.Max(0, value)); }

        // --- Data Source State ---
        private bool _useCustomDept;
        public bool UseCustomDept { get => _useCustomDept; set => Set(ref _useCustomDept, value); }

        private string _deptSource = "Web";
        public string DeptSource { get => _deptSource; set => Set(ref _deptSource, value); }

        private string _deptUri = string.Empty;
        public string DeptUri { get => _deptUri; set => Set(ref _deptUri, value); }

        private bool _useCustomLinks;
        public bool UseCustomLinks { get => _useCustomLinks; set => Set(ref _useCustomLinks, value); }

        private string _linksSource = "Web";
        public string LinksSource { get => _linksSource; set => Set(ref _linksSource, value); }

        private string _linksUri = string.Empty;
        public string LinksUri { get => _linksUri; set => Set(ref _linksUri, value); }

        // --- Tray Settings State ---
        private bool _enableTrayIcon;
        public bool EnableTrayIcon
        {
            get => _enableTrayIcon;
            set
            {
                if (Set(ref _enableTrayIcon, value) && !value)
                {
                    MinimizeToTray = false;
                    CloseToTray = false;
                }
            }
        }

        private bool _minimizeToTray;
        public bool MinimizeToTray { get => _minimizeToTray; set => Set(ref _minimizeToTray, value); }

        private bool _closeToTray;
        public bool CloseToTray { get => _closeToTray; set => Set(ref _closeToTray, value); }

        // --- Update Settings State ---
        private bool _enablePreReleaseChannel;
        public bool EnablePreReleaseChannel { get => _enablePreReleaseChannel; set => Set(ref _enablePreReleaseChannel, value); }

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
            _settings.ApplyDefaultsAndClamp();

            // Load Values -> VM
            DefaultFontSize = _settings.Ui.Font.DefaultSize;
            UsePerViewOverride = _settings.Ui.Font.ViewFontSizeOverride;
            UserFontSize = GetViewSize("UserView", DefaultFontSize);
            ComputerFontSize = GetViewSize("ComputerView", DefaultFontSize);
            GroupFontSize = GetViewSize("GroupView", DefaultFontSize);
            EntraFontSize = GetViewSize("EntraView", DefaultFontSize);

            MinimumLogLevel = _settings.Logging.MinimumLevel;
            RetentionDays = _settings.Logging.RetentionDays;

            UseSavedSearchHistory = _settings.Ui.Search.UseSavedSearchHistory;
            var savedMax = _settings.Ui.Search.MaxSearchHistory;
            EnsureHistoryOption(savedMax);
            MaxSearchHistory = savedMax;

            // Load Data Sources
            var dept = _settings.Paths.DepartmentData;
            UseCustomDept = dept.UseCustomSource;
            // Match loaded value ("web"/"file") to capitalized options ("Web"/"File")
            DeptSource = MatchSourceOption(dept.Source);
            DeptUri = dept.Uri;

            var links = _settings.Paths.LinksData;
            UseCustomLinks = links.UseCustomSource;
            // Match loaded value ("web"/"file") to capitalized options ("Web"/"File")
            LinksSource = MatchSourceOption(links.Source);
            LinksUri = links.Uri;

            // Load Tray Settings
            EnableTrayIcon = _settings.Ui.Tray.EnableTrayIcon;
            MinimizeToTray = _settings.Ui.Tray.MinimizeToTray;
            CloseToTray = _settings.Ui.Tray.CloseToTray;

            // Load Update Settings
            EnablePreReleaseChannel = _settings.Updates.EnablePreReleaseChannel;

            // Configure Commands
            ApplyCommand = new RelayCommand(_ => Apply(deptService, linksService));
            OpenLogsCommand = new RelayCommand(_ => OpenLogsFolder());
            BrowseDeptCommand = new RelayCommand(_ => BrowseForFile(path => DeptUri = path));
            BrowseLinksCommand = new RelayCommand(_ => BrowseForFile(path => LinksUri = path));

            // Runtime policy
            _searchSvc?.ConfigureHistory(UseSavedSearchHistory, MaxSearchHistory);
        }

        // --- Logic ---

        private void Apply(IDepartmentService? deptService, ILinksService? linksService)
        {
            // Use case-insensitive comparison for dirty check
            var deptSettings = _settings.Paths.DepartmentData;
            bool deptChanged = deptSettings.UseCustomSource != UseCustomDept ||
                               !string.Equals(deptSettings.Source, DeptSource, StringComparison.OrdinalIgnoreCase) ||
                               !string.Equals(deptSettings.Uri, DeptUri);

            var linkSettings = _settings.Paths.LinksData;
            bool linksChanged = linkSettings.UseCustomSource != UseCustomLinks ||
                                !string.Equals(linkSettings.Source, LinksSource, StringComparison.OrdinalIgnoreCase) ||
                                !string.Equals(linkSettings.Uri, LinksUri);

            bool trayChanged = _settings.Ui.Tray.EnableTrayIcon != EnableTrayIcon;

            // VM -> Model
            _settings.Ui.Font.DefaultSize = DefaultFontSize;
            _settings.Ui.Font.ViewFontSizeOverride = UsePerViewOverride;

            UpsertViewSize("UserView", UserFontSize);
            UpsertViewSize("ComputerView", ComputerFontSize);
            UpsertViewSize("GroupView", GroupFontSize);
            UpsertViewSize("EntraView", EntraFontSize);

            _settings.Logging.MinimumLevel = MinimumLogLevel;

            var days = RetentionDays;
            if (days == -1) days = 36500;
            if (days < 1) days = 1;
            _settings.Logging.RetentionDays = days;

            if (MaxSearchHistory <= 0)
            {
                _settings.Ui.Search.UseSavedSearchHistory = false;
                if (_settings.Ui.Search.MaxSearchHistory <= 0)
                    _settings.Ui.Search.MaxSearchHistory = 10;
            }
            else
            {
                _settings.Ui.Search.UseSavedSearchHistory = UseSavedSearchHistory;
                EnsureHistoryOption(MaxSearchHistory);
                _settings.Ui.Search.MaxSearchHistory = MaxSearchHistory;
            }

            var dept = _settings.Paths.DepartmentData;
            dept.UseCustomSource = UseCustomDept;
            dept.Source = DeptSource;
            dept.Uri = DeptUri;

            var links = _settings.Paths.LinksData;
            links.UseCustomSource = UseCustomLinks;
            links.Source = LinksSource;
            links.Uri = LinksUri;

            var tray = _settings.Ui.Tray;
            tray.EnableTrayIcon = EnableTrayIcon;
            tray.MinimizeToTray = MinimizeToTray;
            tray.CloseToTray = CloseToTray;

            // VM -> Model for Update Settings
            _settings.Updates.EnablePreReleaseChannel = EnablePreReleaseChannel;

            // Stamp the current schema version before saving so older versions don't overwrite it
            // overwriting whatever Newtonsoft pulled from the old file
            _settings.Meta?.SchemaVersion = Globals.g_SettingsSchema;

            _settings.ApplyDefaultsAndClamp();
            var path = Path.Combine(Globals.g_AppDir, "settings.json");
            _settingsSvc.RequestSave(_settings, path);
            TryFlushPendingSaves(_settingsSvc);

            Log.ApplySettings(_settings);
            _searchSvc?.ConfigureHistory(_settings.Ui.Search.UseSavedSearchHistory, _settings.Ui.Search.MaxSearchHistory);
            if (!_settings.Ui.Search.UseSavedSearchHistory)
                _searchSvc?.ClearHistory();

            if (deptChanged) _ = deptService?.ReloadDataAsync();
            if (linksChanged) _ = linksService?.ReloadLinksDataAsync();

            // Notify application about dynamic tray state changes
            if (trayChanged)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current is App myApp)
                    {
                        myApp.ToggleTrayIcon(EnableTrayIcon);
                    }
                });
            }

            _notifier?.NotifyChanged();
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

            if (dlg.ShowDialog() == true)
            {
                onPathSelected(dlg.FileName);
            }
        }

        // --- Helpers ---

        // Matches "web" -> "Web", "file" -> "File"
        private string MatchSourceOption(string input)
        {
            return DataSourceOptions.FirstOrDefault(x => x.Equals(input, StringComparison.OrdinalIgnoreCase)) ?? "Web";
        }

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
            entry.FontSize = UiLimits.ClampFontSize(size);
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