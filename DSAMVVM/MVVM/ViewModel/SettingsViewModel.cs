using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
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

        // Options for bindings
        public IReadOnlyList<AppLogLevel> LogLevels { get; } =
            [AppLogLevel.Off, AppLogLevel.Debug, AppLogLevel.Info, AppLogLevel.Warn, AppLogLevel.Error];

        public IReadOnlyList<int> RetentionOptions { get; } =
            [7, 14, 30, 90, 180, 365, -1]; // -1 = forever

        // Allow insertion of custom/saved values before selection occurs
        public List<int> HistorySizeOptions { get; } = [0, 5, 10, 15, 20, 25]; // 0 = off

        public IReadOnlyList<double> InitialFontSizeOptions { get; } =
            [10d, 12d, 14d, 16d, 18d, 20d, 22d];

        // Dropdown options for Data Sources
        public ObservableCollection<string> DataSourceOptions { get; } = ["web", "file"];

        // UI state
        private double _defaultFontSize;
        public double DefaultFontSize
        {
            get => _defaultFontSize;
            set
            {
                // Clamp using the helper in AppSettings
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

        // Data source state
        private bool _useCustomDept;
        public bool UseCustomDept { get => _useCustomDept; set => Set(ref _useCustomDept, value); }

        private string _deptSource = "web";
        public string DeptSource { get => _deptSource; set => Set(ref _deptSource, value); }

        private string _deptUri = string.Empty;
        public string DeptUri { get => _deptUri; set => Set(ref _deptUri, value); }

        private bool _useCustomLinks;
        public bool UseCustomLinks { get => _useCustomLinks; set => Set(ref _useCustomLinks, value); }

        private string _linksSource = "web";
        public string LinksSource { get => _linksSource; set => Set(ref _linksSource, value); }

        private string _linksUri = string.Empty;
        public string LinksUri { get => _linksUri; set => Set(ref _linksUri, value); }

        // Commands
        public ICommand ApplyCommand { get; }
        public ICommand OpenLogsCommand { get; }

        // Constructors
        public SettingsViewModel()
            : this(App.Services.GetRequiredService<ISettingsService>(),
                   App.Services.GetService<IOutputTextSettingsProvider>(),
                   App.Services.GetService<ISearchService>())
        { }

        public SettingsViewModel(ISettingsService settingsSvc,
                                 IOutputTextSettingsProvider? notifier,
                                 ISearchService? searchSvc)
        {
            _settingsSvc = settingsSvc;
            _notifier = notifier;
            _searchSvc = searchSvc;

            _settings = App.Settings ?? new AppSettings();
            _settings.ApplyDefaultsAndClamp();

            // load -> vm
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
            EnsureHistoryOption(savedMax); // <- make sure the saved value exists in ItemsSource
            MaxSearchHistory = savedMax;

            // load data sources (fallback to Defaults if empty, but don't force populate fields if unused)
            var dept = _settings.Paths.DepartmentData;
            UseCustomDept = dept.UseCustomSource;
            DeptSource = dept.Source;
            DeptUri = dept.Uri;

            var links = _settings.Paths.LinksData;
            UseCustomLinks = links.UseCustomSource;
            LinksSource = links.Source;
            LinksUri = links.Uri;

            // ensure runtime policy on open
            _searchSvc?.ConfigureHistory(UseSavedSearchHistory, MaxSearchHistory);

            ApplyCommand = new RelayCommand(_ => Apply());
            OpenLogsCommand = new RelayCommand(_ => OpenLogsFolder());
        }

        // Actions
        private void Apply()
        {
            // vm -> model
            _settings.Ui.Font.DefaultSize = DefaultFontSize;
            _settings.Ui.Font.ViewFontSizeOverride = UsePerViewOverride;

            UpsertViewSize("UserView", UserFontSize);
            UpsertViewSize("ComputerView", ComputerFontSize);
            UpsertViewSize("GroupView", GroupFontSize);
            UpsertViewSize("EntraView", EntraFontSize);

            _settings.Logging.MinimumLevel = MinimumLogLevel;

            // Validate retention
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
                EnsureHistoryOption(MaxSearchHistory); // keep options list consistent if custom value chosen
                _settings.Ui.Search.MaxSearchHistory = MaxSearchHistory;
            }

            // save data sources
            var dept = _settings.Paths.DepartmentData;
            dept.UseCustomSource = UseCustomDept;
            dept.Source = DeptSource;
            dept.Uri = DeptUri;

            var links = _settings.Paths.LinksData;
            links.UseCustomSource = UseCustomLinks;
            links.Source = LinksSource;
            links.Uri = LinksUri;

            // Uses AppSettings logic to normalize inputs
            _settings.ApplyDefaultsAndClamp();

            // persist
            var path = Path.Combine(Globals.g_AppDir, "settings.json");
            _settingsSvc.RequestSave(_settings, path);
            TryFlushPendingSaves(_settingsSvc);

            // apply runtime policies
            Log.ApplySettings(_settings);
            _searchSvc?.ConfigureHistory(_settings.Ui.Search.UseSavedSearchHistory, _settings.Ui.Search.MaxSearchHistory);
            if (!_settings.Ui.Search.UseSavedSearchHistory)
                _searchSvc?.ClearHistory();

            _notifier?.NotifyChanged();
        }

        private void OpenLogsFolder()
        {
            var dir = ResolveLogDirCompat(_settingsSvc, _settings);
            try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); } catch { }
        }

        // Helpers
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
                // Reflection: Check if the service supports the newer "ResolveLogDir" method
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