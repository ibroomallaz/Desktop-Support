using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Model.Config.UI;
using DSAMVVM.MVVM.View.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SharpHook.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public class SettingsCategoryItem
    {
        public SettingsCategory Category { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Glyph { get; init; } = string.Empty;

        public override string ToString() => Title;
    }

    public class ModifierKeyOption
    {
        public KeyCode Key { get; init; }
        public string DisplayName { get; init; } = string.Empty;

        public override string ToString() => DisplayName;
    }

    public class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider? _notifier;
        private readonly ISearchService? _searchSvc;

        private AppSettings _settings;

        // --- Navigation / Categories ---
        public IReadOnlyList<SettingsCategoryItem> Categories { get; } =
        [
            new SettingsCategoryItem
            {
                Category = SettingsCategory.Appearance,
                Title = "Appearance & Interface",
                Description = "Search text sizes, per-tab overrides, system tray options, and history limit.",
                Glyph = Glyphs.Appearance
            },
            new SettingsCategoryItem
            {
                Category = SettingsCategory.QuickSearch,
                Title = "Quick Search (Overlay)",
                Description = "Global double-tap hotkey overlay and activation threshold speed.",
                Glyph = Glyphs.QuickSearch
            },
            new SettingsCategoryItem
            {
                Category = SettingsCategory.DataAndLinks,
                Title = "Data & Links",
                Description = "Data source locations (Departments & Links), links start mode, and quick shortcuts.",
                Glyph = Glyphs.DataAndLinks
            },
            new SettingsCategoryItem
            {
                Category = SettingsCategory.SystemAndMaintenance,
                Title = "System & Maintenance",
                Description = "Application update channels, diagnostic logs, and factory reset.",
                Glyph = Glyphs.Maintenance
            }
        ];

        private SettingsCategoryItem _selectedCategory;
        public SettingsCategoryItem SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (Set(ref _selectedCategory, value))
                {
                    OnPropertyChanged(nameof(IsAppearanceSelected));
                    OnPropertyChanged(nameof(IsQuickSearchSelected));
                    OnPropertyChanged(nameof(IsDataAndLinksSelected));
                    OnPropertyChanged(nameof(IsSystemAndMaintenanceSelected));
                }
            }
        }

        public bool IsAppearanceSelected => SelectedCategory.Category == SettingsCategory.Appearance;
        public bool IsQuickSearchSelected => SelectedCategory.Category == SettingsCategory.QuickSearch;
        public bool IsDataAndLinksSelected => SelectedCategory.Category == SettingsCategory.DataAndLinks;
        public bool IsSystemAndMaintenanceSelected => SelectedCategory.Category == SettingsCategory.SystemAndMaintenance;

        public void SelectCategory(SettingsCategory category)
        {
            var match = Categories.FirstOrDefault(x => x.Category == category);
            if (match != null)
            {
                SelectedCategory = match;
            }
        }

        public string? PendingAnchor { get; set; }

        public event Action<string>? ScrollToAnchorRequested;

        public void RequestScrollToAnchor(string anchor)
        {
            PendingAnchor = anchor;
            ScrollToAnchorRequested?.Invoke(anchor);
        }

        public void SelectCategoryAndAnchor(SettingsCategory category, string? anchor = null)
        {
            SelectCategory(category);
            if (!string.IsNullOrWhiteSpace(anchor))
            {
                RequestScrollToAnchor(anchor);
            }
        }

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

        // --- Shortcut Commands ---
        public ICommand AddCustomShortcutCommand { get; }
        public ICommand RemoveShortcutCommand { get; }
        public ICommand MoveShortcutUpCommand { get; }
        public ICommand MoveShortcutDownCommand { get; }
        public ICommand ResetShortcutsCommand { get; }

        // --- Collections ---
        public IReadOnlyList<AppLogLevel> LogLevels { get; } =
            [AppLogLevel.Off, AppLogLevel.Debug, AppLogLevel.Info, AppLogLevel.Warn, AppLogLevel.Error];

        public IReadOnlyList<int> RetentionOptions { get; } =
            [7, 14, 30, 90, 180, 365, -1];

        public IReadOnlyList<ModifierKeyOption> ModifierKeyOptions { get; } =
        [
            new ModifierKeyOption { Key = KeyCode.VcLeftControl, DisplayName = "Left Control" },
            new ModifierKeyOption { Key = KeyCode.VcRightControl, DisplayName = "Right Control" },
            new ModifierKeyOption { Key = KeyCode.VcLeftAlt, DisplayName = "Left Alt" },
            new ModifierKeyOption { Key = KeyCode.VcRightAlt, DisplayName = "Right Alt" },
            new ModifierKeyOption { Key = KeyCode.VcLeftShift, DisplayName = "Left Shift" },
            new ModifierKeyOption { Key = KeyCode.VcRightShift, DisplayName = "Right Shift" }
        ];

        public List<int> HistorySizeOptions { get; } = [0, 5, 10, 15, 20, 25];

        public IReadOnlyList<double> InitialFontSizeOptions { get; } =
            [10d, 12d, 14d, 16d, 18d, 20d, 22d];

        public ObservableCollection<string> DataSourceOptions { get; } = ["Web", "File"];

        // --- Shortcuts Options & State ---
        public ObservableCollection<HomeShortcutItem> Shortcuts { get; } = [];
        public IReadOnlyList<ShortcutGlyphOption> AvailableGlyphs => ShortcutGlyphs.Options;
        public IReadOnlyList<string> AvailableColorPresets => ShortcutColorPresets.Presets;
        public bool CanAddShortcut => Shortcuts.Count < HomeShortcutsSettings.MaxShortcuts;

        private string _newShortcutTitle = string.Empty;
        public string NewShortcutTitle
        {
            get => _newShortcutTitle;
            set => Set(ref _newShortcutTitle, value);
        }

        private string _newShortcutDescription = string.Empty;
        public string NewShortcutDescription
        {
            get => _newShortcutDescription;
            set => Set(ref _newShortcutDescription, value);
        }

        private string _newShortcutTarget = string.Empty;
        public string NewShortcutTarget
        {
            get => _newShortcutTarget;
            set => Set(ref _newShortcutTarget, value);
        }

        private string _newShortcutGlyph = Glyphs.Links;
        public string NewShortcutGlyph
        {
            get => _newShortcutGlyph;
            set => Set(ref _newShortcutGlyph, value);
        }

        private string _newShortcutColor = "Blue";
        public string NewShortcutColor
        {
            get => _newShortcutColor;
            set => Set(ref _newShortcutColor, value);
        }

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

        private bool _enableQuickSearch;
        public bool EnableQuickSearch { get => _enableQuickSearch; set { if (Set(ref _enableQuickSearch, value)) SetModified(); } }

        private KeyCode _quickSearchModifierKey;
        public KeyCode QuickSearchModifierKey
        {
            get => _quickSearchModifierKey;
            set
            {
                if (Set(ref _quickSearchModifierKey, value))
                {
                    if (_selectedModifierKeyOption?.Key != value)
                    {
                        _selectedModifierKeyOption = ModifierKeyOptions.FirstOrDefault(x => x.Key == value);
                        OnPropertyChanged(nameof(SelectedModifierKeyOption));
                    }
                    SetModified();
                }
            }
        }

        private ModifierKeyOption? _selectedModifierKeyOption;
        public ModifierKeyOption? SelectedModifierKeyOption
        {
            get => _selectedModifierKeyOption;
            set
            {
                if (Set(ref _selectedModifierKeyOption, value) && value != null)
                {
                    QuickSearchModifierKey = value.Key;
                }
            }
        }

        private int _quickSearchDoubleTapMs;
        public int QuickSearchDoubleTapMs { get => _quickSearchDoubleTapMs; set { if (Set(ref _quickSearchDoubleTapMs, value)) SetModified(); } }

        //ADMIN UI PROPERTIES
        private bool _hasUnlockedAdmin;
        public bool HasUnlockedAdmin
        {
            get => _hasUnlockedAdmin;
            set => Set(ref _hasUnlockedAdmin, value);
        }

        private bool _showAdminView;
        public bool ShowAdminView
        {
            get => _showAdminView;
            set
            {
                if (Set(ref _showAdminView, value))
                {
                    SetModified();
                }
            }
        }

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

            _settings = App.Settings;
            _selectedCategory = Categories[0];

            LoadValuesFromSettings();

            ApplyCommand = new RelayCommand(_ => Apply(deptService, linksService));
            DiscardCommand = new RelayCommand(_ => LoadValuesFromSettings(), _ => HasUnsavedChanges);
            ResetDefaultsCommand = new RelayCommand(_ => ResetToFactoryDefaults());

            OpenLogsCommand = new RelayCommand(_ => OpenLogsFolder());
            BrowseDeptCommand = new RelayCommand(_ => BrowseForFile(path => DeptUri = path));
            BrowseLinksCommand = new RelayCommand(_ => BrowseForFile(path => LinksUri = path));

            // Shortcuts Commands
            AddCustomShortcutCommand = new RelayCommand(_ => AddCustomShortcut());
            RemoveShortcutCommand = new RelayCommand(p => RemoveShortcut(p as HomeShortcutItem));
            MoveShortcutUpCommand = new RelayCommand(p => MoveShortcutUp(p as HomeShortcutItem));
            MoveShortcutDownCommand = new RelayCommand(p => MoveShortcutDown(p as HomeShortcutItem));
            ResetShortcutsCommand = new RelayCommand(_ => ResetShortcutsToDefaults());
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

            EnableQuickSearch = _settings.QuickSearch.Enabled;
            QuickSearchModifierKey = _settings.QuickSearch.ModifierKeyCode;
            SelectedModifierKeyOption = ModifierKeyOptions.FirstOrDefault(x => x.Key == _settings.QuickSearch.ModifierKeyCode) ?? ModifierKeyOptions[0];
            QuickSearchDoubleTapMs = _settings.QuickSearch.DoubleTapThresholdMs;

            // Load Shortcuts
            Shortcuts.Clear();
            _settings.Ui.Shortcuts.Normalize();
            foreach (var s in _settings.Ui.Shortcuts.Items.OrderBy(x => x.Order))
            {
                var clone = s.Clone();
                clone.PropertyChanged += (_, _) => SetModified();
                Shortcuts.Add(clone);
            }
            OnPropertyChanged(nameof(CanAddShortcut));

            //Load Admin State
            HasUnlockedAdmin = _settings.Ui.HasUnlockedAdmin;
            ShowAdminView = _settings.Ui.ShowAdminView;

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

        private void AddCustomShortcut()
        {
            if (Shortcuts.Count >= HomeShortcutsSettings.MaxShortcuts)
            {
                UiNotify.Warn($"Maximum of {HomeShortcutsSettings.MaxShortcuts} shortcuts reached.");
                return;
            }

            if (string.IsNullOrWhiteSpace(NewShortcutTitle) || string.IsNullOrWhiteSpace(NewShortcutTarget))
            {
                UiNotify.Warn("Please provide both a Title and Target URL/route for the shortcut.");
                return;
            }

            var item = new HomeShortcutItem
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Title = NewShortcutTitle.Trim(),
                Description = NewShortcutDescription.Trim(),
                Target = NewShortcutTarget.Trim(),
                Icon = string.IsNullOrWhiteSpace(NewShortcutGlyph) ? Glyphs.Links : NewShortcutGlyph,
                ColorPreset = ShortcutColorPresets.Normalize(NewShortcutColor),
                IsCustom = true,
                Order = Shortcuts.Count
            };

            item.PropertyChanged += (_, _) => SetModified();
            Shortcuts.Add(item);
            OnPropertyChanged(nameof(CanAddShortcut));

            NewShortcutTitle = string.Empty;
            NewShortcutDescription = string.Empty;
            NewShortcutTarget = string.Empty;
            NewShortcutGlyph = Glyphs.Links;
            NewShortcutColor = "Blue";

            SetModified();
            UiNotify.Info($"Added shortcut '{item.Title}'. Click Apply to save.", showStatusBar: true);
        }

        private void RemoveShortcut(HomeShortcutItem? item)
        {
            if (item == null) return;
            if (Shortcuts.Remove(item))
            {
                for (int i = 0; i < Shortcuts.Count; i++) Shortcuts[i].Order = i;
                OnPropertyChanged(nameof(CanAddShortcut));
                SetModified();
            }
        }

        private void MoveShortcutUp(HomeShortcutItem? item)
        {
            if (item == null) return;
            int idx = Shortcuts.IndexOf(item);
            if (idx > 0)
            {
                Shortcuts.Move(idx, idx - 1);
                for (int i = 0; i < Shortcuts.Count; i++) Shortcuts[i].Order = i;
                SetModified();
            }
        }

        private void MoveShortcutDown(HomeShortcutItem? item)
        {
            if (item == null) return;
            int idx = Shortcuts.IndexOf(item);
            if (idx >= 0 && idx < Shortcuts.Count - 1)
            {
                Shortcuts.Move(idx, idx + 1);
                for (int i = 0; i < Shortcuts.Count; i++) Shortcuts[i].Order = i;
                SetModified();
            }
        }

        private void ResetShortcutsToDefaults()
        {
            var res = System.Windows.MessageBox.Show(
                "Reset Quick Shortcuts to factory defaults?",
                "Reset Shortcuts", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

            if (res != System.Windows.MessageBoxResult.Yes) return;

            Shortcuts.Clear();
            var defaults = new HomeShortcutsSettings();
            defaults.Normalize();
            foreach (var s in defaults.Items)
            {
                s.PropertyChanged += (_, _) => SetModified();
                Shortcuts.Add(s);
            }
            OnPropertyChanged(nameof(CanAddShortcut));
            SetModified();
            UiNotify.Info("Shortcuts reset to defaults. Click Apply to save.", showStatusBar: true);
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

                _settings.QuickSearch.Enabled = EnableQuickSearch;
                _settings.QuickSearch.ModifierKeyCode = SelectedModifierKeyOption?.Key ?? QuickSearchModifierKey;
                _settings.QuickSearch.DoubleTapThresholdMs = QuickSearchDoubleTapMs;

                // Sync Shortcuts
                _settings.Ui.Shortcuts.Items = Shortcuts.Select((s, idx) =>
                {
                    var clone = s.Clone();
                    clone.Order = idx;
                    return clone;
                }).ToList();
                _settings.Ui.Shortcuts.Normalize();

                // Sync Admin State
                _settings.Ui.HasUnlockedAdmin = HasUnlockedAdmin;
                _settings.Ui.ShowAdminView = ShowAdminView;

                _settings.ApplyDefaultsAndClamp();

                // 3. Transfer the data to the global reference.
                active.Ui = _settings.Ui;
                active.Paths = _settings.Paths;
                active.Logging = _settings.Logging;
                active.Updates = _settings.Updates;
                active.Meta = _settings.Meta;
                active.QuickSearch = _settings.QuickSearch;

                // 4. Save the global object to disk (FIXED PATH)
                await _settingsSvc.SaveAsync(active, Globals.g_SettingsPath);

                // 5. Update UI state
                HasUnsavedChanges = false;
                StatusMessage = "Settings successfully applied!";
                UiNotify.Info("Settings applied.", showStatusBar: true);

                // 6. Trigger Side Effects
                Log.ApplySettings(active);
                _searchSvc?.ConfigureHistory(active.Ui.Search.UseSavedSearchHistory, active.Ui.Search.MaxSearchHistory);
                if (!active.Ui.Search.UseSavedSearchHistory) _searchSvc?.ClearHistory();

                var quickSearchSvc = App.Services.GetService<IQuickSearchService>();
                quickSearchSvc?.Configure(active.QuickSearch);

                if (deptChanged) _ = deptService?.ReloadDataAsync();
                if (linksChanged) _ = linksService?.ReloadLinksDataAsync();

                if (trayChanged)
                {
                    UiNotify.RunOnUi(() =>
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
            catch (Exception ex)
            {
                StatusMessage = "Error saving settings.";
                UiNotify.Error("Settings Error", ex.Message, alsoStatusBar: true);
                Log.Error("Settings", "Failed to apply settings", ex);
            }
        }

        private void OpenLogsFolder()
        {
            var dir = ResolveLogDirCompat(_settingsSvc, _settings);
            try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); }
            catch
            {
                // ignored
            }
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
            if (_settings.Ui.ViewFontSizes.TryGetValue(key, out var entry) && entry is { FontSize: > 0 })
                return UiLimits.ClampFontSize(entry.FontSize);
            return UiLimits.ClampFontSize(fallback);
        }

        private void UpsertViewSize(string key, double size)
        {
            if (!_settings.Ui.ViewFontSizes.TryGetValue(key, out var entry))
                _settings.Ui.ViewFontSizes[key] = entry = new ViewFontSetting();

            if (entry.FontSize == size) return;
            entry.FontSize = UiLimits.ClampFontSize(size);
            SetModified();
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
            catch
            {
                // ignored
            }
        }

        private static string ResolveLogDirCompat(ISettingsService svc, AppSettings s)
        {
            try
            {
                var methodInfo = svc.GetType().GetMethod("ResolveLogDir", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (methodInfo != null)
                {
                    var val = methodInfo.Invoke(svc, [s]) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
            catch
            {
                // ignored
            }

            Directory.CreateDirectory(Globals.g_LogsDir);
            return Globals.g_LogsDir;
        }
    }
}
