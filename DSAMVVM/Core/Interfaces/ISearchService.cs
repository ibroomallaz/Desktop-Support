using System.Collections.ObjectModel;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Interfaces
{
    public interface ISearchService
    {
        Task<object?> SearchAsync(SearchContextDTO context, SearchTarget target);

        // History collection for direct UI binding
        ReadOnlyObservableCollection<string> SearchHistory { get; }
        ReadOnlyObservableCollection<SearchHistoryEntry> RecentSearches { get; }

        // History policy
        void AddToHistory(string query);
        void AddToHistory(string query, SearchTarget target);
        void ConfigureHistory(bool enabled, int capacity);
        void ClearHistory();

        // History access
        IReadOnlyList<string> GetHistorySnapshot();
        IReadOnlyList<SearchHistoryEntry> GetRecentSearchesSnapshot();

        // Notification for UI sync
        event EventHandler? HistoryChanged;
    }
}
