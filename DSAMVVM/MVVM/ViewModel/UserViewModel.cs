using DSAMVVM.MVVM.ViewModel.Cards;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using DSAMVVM.MVVM.Model.Config.UI;

namespace DSAMVVM.MVVM.ViewModel
{
    public class UserViewModel : ObservableObject, ISearchableViewModel, IDisposable
    {
        private readonly IADService _adService;
        private readonly IDepartmentService _deptService;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;
        private readonly IDeepLinkRoutingService _linkRouter;

        private const string ViewKey = "UserView";

        public ObservableCollection<UserHistoryItemViewModel> History { get; } = new();

        public bool HasHistory => History.Count > 0;

        private double _effectiveFontSize = UiLimits.DefaultFontSize;
        public double EffectiveFontSize
        {
            get => _effectiveFontSize;
            private set
            {
                if (Math.Abs(_effectiveFontSize - value) > 0.01)
                {
                    _effectiveFontSize = value;
                    OnPropertyChanged(nameof(EffectiveFontSize));
                    OnPropertyChanged(nameof(Typography));
                }
            }
        }

        public CardTypography Typography => CardTypography.FromBase(EffectiveFontSize);

        private string? _error;
        public string? Error
        {
            get => _error;
            private set { _error = value; OnPropertyChanged(nameof(Error)); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set { _isRefreshing = value; OnPropertyChanged(nameof(IsRefreshing)); }
        }

        // Commands for modern UI
        public ICommand ClearCommand { get; }
        public ICommand RefreshDeptCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand RemoveHistoryItemCommand { get; }
        public ICommand IncreaseFontCommand { get; }
        public ICommand DecreaseFontCommand { get; }
        public ICommand ResetFontCommand { get; }

        public UserViewModel(
            IADService adService,
            IDepartmentService deptService,
            ISettingsService settingsSvc,
            IOutputTextSettingsProvider notifier,
            IDeepLinkRoutingService linkRouter)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            ClearCommand = new RelayCommand(_ => ClearLog());
            RefreshDeptCommand = new RelayCommand(async _ => await RefreshDepartmentDataAsync());
            CollapseAllCommand = new RelayCommand(_ => CollapseAll());
            ExpandAllCommand = new RelayCommand(_ => ExpandAll());
            RemoveHistoryItemCommand = new RelayCommand(param =>
            {
                if (param is UserHistoryItemViewModel item)
                {
                    History.Remove(item);
                }
            });
            IncreaseFontCommand = new RelayCommand(_ => AdjustFont(+1));
            DecreaseFontCommand = new RelayCommand(_ => AdjustFont(-1));
            ResetFontCommand = new RelayCommand(_ => ResetFont());

            History.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasHistory));

            _notifier.Changed += OnFontSettingsChanged;

            RefreshEffectiveFontSize();
        }

        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;

            if (target != SearchTarget.User || string.IsNullOrWhiteSpace(context.Query))
            {
                Error = "Invalid target or empty query.";
                Log.Warn(ViewKey, $"Search aborted: Invalid target ({target}) or empty query.");
                return;
            }

            try
            {
                IsLoading = true;
                Log.Info(ViewKey, $"Starting User search for '{context.Query}'");

                var user = await searchService.SearchAsync(context, target) as ADUserInfo;

                // Collapse all previous items so newest expands
                foreach (var item in History)
                {
                    item.IsExpanded = false;
                }

                var entry = new UserHistoryItemViewModel(
                    context.Query,
                    user,
                    _adService,
                    _deptService,
                    _linkRouter)
                {
                    IsExpanded = true
                };

                if (user is { Exists: true })
                {
                    Log.Info(ViewKey, $"User '{user.Name}' found successfully.");

                    await entry.LoadDepartmentDetailsAsync();
                }
                else
                {
                    Error = user?.ErrorMessage ?? "User not found.";
                    Log.Warn(ViewKey, $"Search completed, but user '{context.Query}' was not found. Error: {Error}");
                }

                AddHistoryEntry(entry);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Log.Error(ViewKey, $"Exception during user search for '{context.Query}'", ex);

                var failEntry = new UserHistoryItemViewModel(
                    context.Query,
                    new ADUserInfo { Name = context.Query, Exists = false, ErrorMessage = ex.Message },
                    _adService,
                    _deptService,
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

        private void AddHistoryEntry(UserHistoryItemViewModel entry)
        {
            entry.RemoveRequested += item => History.Remove(item);
            History.Add(entry);
        }

        public async Task RefreshDepartmentDataAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            try
            {
                UiNotify.Info("Refreshing department data…", showStatusBar: true, key: "DeptRefresh");
                await _deptService.ReloadDataAsync();

                // Refresh department info for any currently displayed users
                foreach (var item in History)
                {
                    if (item.IsFound)
                    {
                        await item.LoadDepartmentDetailsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ViewKey, $"Refresh failed: {ex.Message}", ex);
            }
            finally { IsRefreshing = false; }
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

        public void AdjustFont(int delta)
        {
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;
            _settingsSvc.AdjustOutputFontSize(s, perView ? ViewKey : null, delta, perView);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        public void ResetFont()
        {
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;
            _settingsSvc.ResetOutputFontSize(s, perView ? ViewKey : null, perView, (int)UiLimits.DefaultFontSize);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        public Task<string?> LookupNameByID(string id) => _adService.LookupNameByEmployeeID(id);

        public void ClearLog()
        {
            History.Clear();
        }

        private void OnFontSettingsChanged(object? s, EventArgs e) => RefreshEffectiveFontSize();
        private void RefreshEffectiveFontSize() => EffectiveFontSize = _notifier.GetFontSize(ViewKey);

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;

            _notifier.Changed -= OnFontSettingsChanged;

            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
