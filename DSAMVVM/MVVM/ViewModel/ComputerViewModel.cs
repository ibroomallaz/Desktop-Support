using DSAMVVM.MVVM.ViewModel.Cards;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.MVVM.ViewModel
{
    public class ComputerViewModel : ObservableObject, ISearchableViewModel, IDisposable
    {
        // Services
        private readonly IADService _ad;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;
        private readonly IFlowDocService _flowDoc;
        private readonly IDeepLinkRoutingService _linkRouter;

        private const string ViewKey = "ComputerView";

        // Modern Card Feed History
        public ObservableCollection<ComputerHistoryItemViewModel> History { get; } = new();
        public bool HasHistory => History.Count > 0;

        // Errors / state
        private string? _error;
        public string? Error { get => _error; private set { _error = value; OnPropertyChanged(); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(); } }

        // FlowDoc text (retained internally for backward compatibility)
        private string _searchLog = string.Empty;
        public string SearchLog { get => _searchLog; private set { _searchLog = value; OnPropertyChanged(); } }

        // Effective output font size for this view
        private double _effectiveOutputFontSize;
        public double EffectiveOutputFontSize
        {
            get => _effectiveOutputFontSize;
            private set { _effectiveOutputFontSize = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand ClearCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand RemoveHistoryItemCommand { get; }
        public ICommand IncreaseFontCommand { get; }
        public ICommand DecreaseFontCommand { get; }
        public ICommand ResetFontCommand { get; }

        public ComputerViewModel(
            IADService adService,
            ISettingsService settingsSvc,
            IOutputTextSettingsProvider notifier,
            IFlowDocService flowDoc,
            IDeepLinkRoutingService linkRouter)
        {
            _ad = adService ?? throw new ArgumentNullException(nameof(adService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _flowDoc = flowDoc ?? throw new ArgumentNullException(nameof(flowDoc));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            ClearCommand = new RelayCommand(_ => ClearLog());
            ClearLogCommand = new RelayCommand(_ => ClearLog());
            CollapseAllCommand = new RelayCommand(_ => CollapseAll());
            ExpandAllCommand = new RelayCommand(_ => ExpandAll());
            RemoveHistoryItemCommand = new RelayCommand(param =>
            {
                if (param is ComputerHistoryItemViewModel item)
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
            _flowDoc.LinkClicked += OnLinkClicked;
        }

        // --- Deep Link Handler ---
        private async void OnLinkClicked(object? sender, string url)
        {
            string? sourceView = sender as string;
            if (sourceView != ViewKey) return;

            if (IsLoading) return;

            string result = await _linkRouter.HandleLinkAsync(url);
            if (!string.IsNullOrWhiteSpace(result))
            {
                SearchLog += result;
            }
        }

        private void OnOutputFontSettingsChanged(object? sender, EventArgs e) => RefreshEffectiveFont();

        private void RefreshEffectiveFont()
        {
            EffectiveOutputFontSize = _notifier.GetFontSize(ViewKey);
        }

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

            _settingsSvc.ResetOutputFontSize(s, preferPerView ? ViewKey : null, preferPerView, defaultSize: 14);
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));

            _notifier.NotifyChanged();
        }

        // --- Search Flow ---
        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;

            var headerDoc = new FlowDocMarkupBuilder();
            if (!string.IsNullOrEmpty(SearchLog)) headerDoc.AddHeader("New Search");
            if (!string.IsNullOrWhiteSpace(context.Query)) headerDoc.AddLabelValue("Query: ", context.Query);

            SearchLog += headerDoc.ToString();

            if (target != SearchTarget.Computer || string.IsNullOrWhiteSpace(context.Query))
            {
                Error = "Invalid target or empty query.";
                Log.Warn(ViewKey, $"Search aborted: Invalid target ({target}) or empty query.");

                var errDoc = new FlowDocMarkupBuilder();
                errDoc.AddError("Aborted: Invalid search parameters.");
                SearchLog += errDoc.ToString();
                return;
            }

            try
            {
                IsLoading = true;
                Log.Info(ViewKey, $"Starting Computer search for '{context.Query}'");

                var result = await searchService.SearchAsync(context, target);
                var comp = result as ADComputerInfo;

                // Push formatting to the legacy renderer
                SearchLog += IdentityRenderer.RenderADComputer(comp);

                // Collapse previous items so newest expands
                foreach (var item in History)
                {
                    item.IsExpanded = false;
                }

                var entry = new ComputerHistoryItemViewModel(
                    context.Query,
                    comp,
                    _ad,
                    _linkRouter)
                {
                    IsExpanded = true
                };

                if (comp is { Exists: true })
                {
                    Log.Info(ViewKey, $"Computer '{comp.Name}' found successfully.");
                }
                else
                {
                    Error = comp?.ErrorMessage ?? "Computer not found.";
                    Log.Warn(ViewKey, $"Search completed, but computer '{context.Query}' was not found. Error: {Error}");
                }

                AddHistoryEntry(entry);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Log.Error(ViewKey, $"Exception during computer search for '{context.Query}'", ex);

                var failDoc = new FlowDocMarkupBuilder();
                failDoc.AddError($"Search failed: {ex.Message}");
                SearchLog += failDoc.ToString();

                var failEntry = new ComputerHistoryItemViewModel(
                    context.Query,
                    new ADComputerInfo { Name = context.Query, Exists = false, ErrorMessage = ex.Message },
                    _ad,
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

        private void AddHistoryEntry(ComputerHistoryItemViewModel entry)
        {
            entry.RemoveRequested += item => History.Remove(item);
            History.Add(entry);
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
            SearchLog = string.Empty;
            History.Clear();
        }

        // Dispose pattern
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
                _notifier.Changed -= OnOutputFontSettingsChanged;
                _flowDoc.LinkClicked -= OnLinkClicked;
            }
            _disposed = true;
        }
    }
}
