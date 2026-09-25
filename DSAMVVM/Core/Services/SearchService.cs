using System.Collections.ObjectModel;
using System.Windows;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.MVVM.Model.Config;

namespace DSAMVVM.Core.Services
{
    public class SearchService : ISearchService
    {
        private readonly IADService _ad;
        private readonly IDepartmentService _dept;
        private readonly IApplicationStateService _appStateService;

        // History state
        private readonly Lock _gate = new();
        private bool _historyEnabled = true;
        private int _historyCap = 10;
        private readonly List<string> _history = [];
        private readonly List<SearchHistoryEntry> _entries = [];
        private readonly ObservableCollection<string> _searchHistory = [];
        private readonly ObservableCollection<SearchHistoryEntry> _recentSearches = [];

        public ReadOnlyObservableCollection<string> SearchHistory { get; }
        public ReadOnlyObservableCollection<SearchHistoryEntry> RecentSearches { get; }

        public event EventHandler? HistoryChanged;

        public SearchService(
            IADService ad,
            IDepartmentService dept,
            IApplicationStateService appStateService,
            AppSettings? settings = null)
        {
            _ad = ad ?? throw new ArgumentNullException(nameof(ad));
            _dept = dept ?? throw new ArgumentNullException(nameof(dept));
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));

            SearchHistory = new ReadOnlyObservableCollection<string>(_searchHistory);
            RecentSearches = new ReadOnlyObservableCollection<SearchHistoryEntry>(_recentSearches);

            if (settings?.Ui?.Search != null)
            {
                _historyEnabled = settings.Ui.Search.UseSavedSearchHistory;
                _historyCap = Math.Max(0, settings.Ui.Search.MaxSearchHistory);
            }
        }

        public void ConfigureHistory(bool enabled, int capacity)
        {
            lock (_gate)
            {
                _historyEnabled = enabled;
                _historyCap = Math.Max(0, capacity);
                if (!_historyEnabled || _historyCap == 0)
                {
                    if (_history.Count > 0 || _entries.Count > 0)
                    {
                        _history.Clear();
                        _entries.Clear();
                        DispatchHistoryUpdate();
                    }
                    return;
                }
                TrimToCap_NoLock();
            }
            DispatchHistoryUpdate();
        }

        public void ClearHistory()
        {
            lock (_gate)
            {
                if (_history.Count == 0 && _entries.Count == 0) return;
                _history.Clear();
                _entries.Clear();
            }
            DispatchHistoryUpdate();
        }

        public IReadOnlyList<string> GetHistorySnapshot()
        {
            lock (_gate)
                return [.. _history];
        }

        public IReadOnlyList<SearchHistoryEntry> GetRecentSearchesSnapshot()
        {
            lock (_gate)
                return [.. _entries];
        }

        public async Task<object?> SearchAsync(SearchContextDTO? context, SearchTarget target)
        {
            if (string.IsNullOrWhiteSpace(context?.Query))
                throw new ArgumentException("Search query cannot be empty.", nameof(context));

            AddToHistory(context.Query, target);

            return target switch
            {
                SearchTarget.User => await _ad.GetUserAsync(context.Query),
                SearchTarget.Computer => await _ad.GetComputerAsync(context.Query),
                SearchTarget.Group => await _ad.GetGroupAsync(context.Query),
                SearchTarget.Admin => context.Mode switch
                {
                    nameof(AdminSection.Department) => await _dept.GetDepartmentAsync(context.Query),
                    nameof(AdminSection.SupportTeam) => await _dept.GetSupportTeamAsync(context.Query),
                    _ => throw new NotSupportedException($"Admin search mode '{context.Mode}' is not supported.")
                },
                _ => throw new NotSupportedException($"Search not implemented for '{target}'.")
            };
        }

        public void AddToHistory(string query) => AddToHistory(query, SearchTarget.User);

        public void AddToHistory(string query, SearchTarget target)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            string formattedQuery = query.Trim();

            _appStateService.RecentQuery = formattedQuery;

            lock (_gate)
            {
                if (!_historyEnabled || _historyCap <= 0) return;

                // Update string history
                int existingIdx = _history.FindIndex(q => string.Equals(q, formattedQuery, StringComparison.OrdinalIgnoreCase));
                if (existingIdx >= 0) _history.RemoveAt(existingIdx);
                _history.Insert(0, formattedQuery);

                // Update recent search entries
                int existingEntryIdx = _entries.FindIndex(e =>
                    e.Target == target && string.Equals(e.Query, formattedQuery, StringComparison.OrdinalIgnoreCase));
                if (existingEntryIdx >= 0) _entries.RemoveAt(existingEntryIdx);
                _entries.Insert(0, new SearchHistoryEntry(formattedQuery, target, DateTime.UtcNow));

                TrimToCap_NoLock();
            }
            DispatchHistoryUpdate();
        }

        private void TrimToCap_NoLock()
        {
            if (_historyCap <= 0)
            {
                if (_history.Count > 0) _history.Clear();
                if (_entries.Count > 0) _entries.Clear();
                return;
            }
            if (_history.Count > _historyCap)
                _history.RemoveRange(_historyCap, _history.Count - _historyCap);
            if (_entries.Count > _historyCap)
                _entries.RemoveRange(_historyCap, _entries.Count - _historyCap);
        }

        private void DispatchHistoryUpdate()
        {
            var stringSnapshot = GetHistorySnapshot();
            var entrySnapshot = GetRecentSearchesSnapshot();

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => UpdateObservableHistoryCore(stringSnapshot, entrySnapshot));
            }
            else
            {
                UpdateObservableHistoryCore(stringSnapshot, entrySnapshot);
            }
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateObservableHistoryCore(IReadOnlyList<string> items, IReadOnlyList<SearchHistoryEntry> entries)
        {
            _searchHistory.Clear();
            foreach (var item in items)
            {
                _searchHistory.Add(item);
            }

            _recentSearches.Clear();
            foreach (var entry in entries)
            {
                _recentSearches.Add(entry);
            }
        }
    }
}
