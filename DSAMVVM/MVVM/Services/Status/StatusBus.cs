using System.Windows;
using System.Windows.Threading;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;

namespace DSAMVVM.MVVM.Services.Status
{
    public sealed class StatusBus
    {
        public StatusItem? Current { get; private set; }
        public event EventHandler? CurrentChanged;
        private readonly IApplicationStateService _appStateService;

        // timing/behavior
        public bool AutoRotateEnabled { get; set; } = true;   // master on/off
        public double SecondsPerItem { get; set; } = 6.0;     // base dwell per item
        public double MinSecondsPerItem { get; set; } = 2.0;  // dwell floor
        public bool StickyPinsRotation { get; set; } = true;  // stickies pin immediately
        public bool RequeueInterruptedNonSticky { get; set; } = true; // put interrupted item back at front

        // non-sticky FIFO and keyed lookup
        private readonly List<StatusItem> _queue = [];
        private readonly Dictionary<string, StatusItem> _byKey =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly DispatcherTimer _timer;

        public StatusBus(IApplicationStateService appStateService)
        {
            _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));
            _timer = new DispatcherTimer { IsEnabled = false };
            _timer.Tick += (_, __) => Advance();
        }

        public void Report(StatusItem item)
        {
            void OnUi()
            {
                if (item.Level == StatusLevel.Error)
                {
                    // Stitches the text from all formatting spans into a single plain-text string
                    _appStateService.RecentError = string.Join("", item.Spans.Select(s => s.Text));
                }
                if (!string.IsNullOrEmpty(item.Key))
                    _byKey[item.Key!] = item;

                // Resolution: non-sticky with same key as a pinned sticky replaces it now
                if (Current != null && Current.Sticky && !item.Sticky &&
                    !string.IsNullOrEmpty(Current.Key) &&
                    string.Equals(Current.Key, item.Key, StringComparison.OrdinalIgnoreCase))
                {
                    StopTimer();
                    if (!string.IsNullOrEmpty(Current.Key)) _byKey.Remove(Current.Key!); // drop old sticky key
                    SetCurrent(item);          // show success/info now
                    StartTimerIfNeeded();      // then continue single-pass queue after dwell
                    return;
                }

                // Sticky pins and (optionally) preserves interrupted non-sticky by re-queuing to front
                if (item.Sticky && StickyPinsRotation)
                {
                    if (RequeueInterruptedNonSticky && Current != null && !Current.Sticky)
                        RequeueAtFront(Current);

                    StopTimer();
                    SetCurrent(item);
                    return;
                }

                // Non-sticky updates
                if (!string.IsNullOrEmpty(item.Key))
                {
                    // Update currently showing non-sticky with same key
                    if (Current != null &&
                        !Current.Sticky &&
                        string.Equals(Current.Key, item.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        SetCurrent(item);
                        StartTimerIfNeeded();
                        return;
                    }

                    // Update queued item with same key, else enqueue to tail
                    int queueIndex = _queue.FindIndex(q =>
                        string.Equals(q.Key, item.Key, StringComparison.OrdinalIgnoreCase));
                    if (queueIndex >= 0)
                        _queue[queueIndex] = item;
                    else
                        _queue.Add(item);
                }
                else
                {
                    // Unkeyed non-sticky — just enqueue
                    _queue.Add(item);
                }

                // If idle, start queue; else ensure timer running
                if (Current == null)
                {
                    if (DequeueToCurrentOrClear())
                        StartTimerIfNeeded();
                }
                else
                {
                    StartTimerIfNeeded();
                }
            }

            var d = Application.Current?.Dispatcher;
            if (d != null && !d.CheckAccess()) d.Invoke(OnUi); else OnUi();
        }

        public void RemoveByKey(string key)
        {
            void OnUi()
            {
                _byKey.Remove(key);

                // If removing what's on screen, clear it and continue queue
                if (Current != null && string.Equals(Current.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    SetCurrent(null);

                    if (DequeueToCurrentOrClear())
                        StartTimerIfNeeded();
                    else
                        StopTimer();

                    return;
                }

                // Remove from queue
                int queueIndex = _queue.FindIndex(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
                if (queueIndex >= 0) _queue.RemoveAt(queueIndex);

                // If idle and we have items, start them
                if (Current == null && DequeueToCurrentOrClear())
                    StartTimerIfNeeded();
            }

            var d = Application.Current?.Dispatcher;
            if (d != null && !d.CheckAccess()) d.Invoke(OnUi); else OnUi();
        }

        public void Clear()
        {
            void OnUi()
            {
                StopTimer();
                _queue.Clear();
                _byKey.Clear();
                SetCurrent(null);
            }

            var d = Application.Current?.Dispatcher;
            if (d != null && !d.CheckAccess()) d.Invoke(OnUi); else OnUi();
        }

        private void Advance()
        {
            // stickies are not timed
            if (Current != null && Current.Sticky && StickyPinsRotation)
            {
                StopTimer();
                return;
            }

            // next item or clear
            if (!DequeueToCurrentOrClear())
            {
                StopTimer();
            }
            else
            {
                StartTimerIfNeeded();
            }
        }

        private bool DequeueToCurrentOrClear()
        {
            if (_queue.Count == 0)
            {
                SetCurrent(null);
                return false;
            }

            var next = _queue[0];
            _queue.RemoveAt(0);
            SetCurrent(next);
            return true;
        }

        private void StartTimerIfNeeded()
        {
            if (!AutoRotateEnabled) return;
            if (Current == null) { StopTimer(); return; }

            if (Current.Sticky && StickyPinsRotation)
            {
                StopTimer();
                return;
            }

            // Faster dwell with more items remaining; last item gets the longest.
            int remainingCount = _queue.Count + 1;
            double seconds = Math.Max(MinSecondsPerItem, SecondsPerItem - (remainingCount - 1));
            _timer.Interval = TimeSpan.FromSeconds(seconds);

            // restart to apply new interval immediately
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _timer.Start();
            }
            else
            {
                _timer.Start();
            }
        }

        private void StopTimer() => _timer.Stop();

        private void SetCurrent(StatusItem? msg)
        {
            if (!ReferenceEquals(Current, msg))
            {
                Current = msg;
                CurrentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Put an interrupted non-sticky back at the front,
        // updating an existing same-key entry if needed to avoid duplicates.
        private void RequeueAtFront(StatusItem interrupted)
        {
            if (!string.IsNullOrEmpty(interrupted.Key))
            {
                int existing = _queue.FindIndex(m =>
                    string.Equals(m.Key, interrupted.Key, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0) _queue.RemoveAt(existing);
            }
            _queue.Insert(0, interrupted);
        }
    }
}
