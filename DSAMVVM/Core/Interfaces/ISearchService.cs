using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Interfaces
{
    public interface ISearchService
    {
        Task<object?> SearchAsync(SearchContextDTO context, SearchTarget target);

        // History policy
        void AddToHistory(string query);
        void ConfigureHistory(bool enabled, int capacity);
        void ClearHistory();

        // History access
        IReadOnlyList<string> GetHistorySnapshot();

        // Notification for UI sync
        event EventHandler? HistoryChanged;
    }
}
