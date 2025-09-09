using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    public class SearchService : ISearchService
    {
        private readonly IADService _ad;

        // History state
        private readonly object _gate = new();
        private bool _historyEnabled = true;
        private int _historyCap = 10;
        private readonly List<string> _history = new();

        public event EventHandler? HistoryChanged;

        public SearchService(IADService ad)
        {
            _ad = ad ?? throw new ArgumentNullException(nameof(ad));
        }

        public void ConfigureHistory(bool enabled, int capacity)
        {
            lock (_gate)
            {
                _historyEnabled = enabled;
                _historyCap = Math.Max(0, capacity);
                if (!_historyEnabled || _historyCap == 0)
                {
                    if (_history.Count > 0)
                    {
                        _history.Clear();
                        HistoryChanged?.Invoke(this, EventArgs.Empty);
                    }
                    return;
                }
                TrimToCap_NoLock();
            }
        }

        public void ClearHistory()
        {
            lock (_gate)
            {
                if (_history.Count == 0) return;
                _history.Clear();
            }
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<string> GetHistorySnapshot()
        {
            lock (_gate)
                return _history.ToList();
        }

        public async Task<object?> SearchAsync(SearchContextDTO context, SearchTarget target)
        {
            if (string.IsNullOrWhiteSpace(context?.Query))
                throw new ArgumentException("Search query cannot be empty.", nameof(context));

            MaybeAddToHistory(context.Query);

            return target switch
            {
                SearchTarget.User => await _ad.GetUserAsync(context.Query),
                SearchTarget.Computer => await _ad.GetComputerAsync(context.Query),
                SearchTarget.Group => await _ad.GetGroupAsync(context.Query),
                _ => throw new NotSupportedException($"Search not implemented for '{target}'.")
            };
        }

        private void MaybeAddToHistory(string query)
        {
            lock (_gate)
            {
                if (!_historyEnabled || _historyCap <= 0) return;

                // de-dup case-insensitive, move to front
                int existing = _history.FindIndex(q => string.Equals(q, query, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0) _history.RemoveAt(existing);
                _history.Insert(0, query);

                TrimToCap_NoLock();
            }
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TrimToCap_NoLock()
        {
            if (_historyCap <= 0)
            {
                if (_history.Count > 0) _history.Clear();
                return;
            }
            if (_history.Count > _historyCap)
                _history.RemoveRange(_historyCap, _history.Count - _historyCap);
        }
    }
}
