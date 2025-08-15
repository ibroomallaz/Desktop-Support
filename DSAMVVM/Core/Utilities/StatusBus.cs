using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using DSAMVVM.MVVM.Model;

namespace DSAMVVM.Core.Utilities
{
    public sealed class StatusBus
    {
        public StatusMessage? Current { get; private set; }
        public event EventHandler? CurrentChanged;

        public bool AutoRotateEnabled { get; set; } = true;   // master on/off
        public double SecondsPerItem { get; set; } = 6.0;     // base dwell per item
        public double MinSecondsPerItem { get; set; } = 2.0;  // dwell floor
        public bool StickyPinsRotation { get; set; } = true;  // stickies pin

        private readonly List<StatusMessage> _queue = new();  // non-sticky FIFO
        private readonly Dictionary<string, StatusMessage> _byKey =
            new(StringComparer.OrdinalIgnoreCase);            // track keyed msgs

        private readonly DispatcherTimer _timer;

        public StatusBus()
        {
            _timer = new DispatcherTimer { IsEnabled = false };
            _timer.Tick += (_, __) => Advance();
        }

        public void Report(StatusMessage msg)
        {
            void OnUi()
            {
                // store/update keyed reference
                if (!string.IsNullOrEmpty(msg.Key))
                    _byKey[msg.Key!] = msg;

                // stickies pin immediately
                if (msg.Sticky && StickyPinsRotation)
                {
                    StopTimer();
                    SetCurrent(msg);
                    return;
                }

                // keyed update to current non-sticky
                if (!string.IsNullOrEmpty(msg.Key))
                {
                    if (Current != null &&
                        !Current.Sticky &&
                        string.Equals(Current.Key, msg.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        SetCurrent(msg);
                        StartTimerIfNeeded();
                        return;
                    }

                    // keyed update in queue or enqueue new
                    int queueIndex = _queue.FindIndex(m =>
                        string.Equals(m.Key, msg.Key, StringComparison.OrdinalIgnoreCase));
                    if (queueIndex >= 0)
                        _queue[queueIndex] = msg;
                    else
                        _queue.Add(msg);
                }
                else
                {
                    // unkeyed non-sticky
                    _queue.Add(msg);
                }

                // start queue if idle
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

                // remove if current
                if (Current != null && string.Equals(Current.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    SetCurrent(null);

                    if (DequeueToCurrentOrClear())
                        StartTimerIfNeeded();
                    else
                        StopTimer();

                    return;
                }

                // remove from queue
                int queueIndex = _queue.FindIndex(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
                if (queueIndex >= 0) _queue.RemoveAt(queueIndex);

                // start if idle
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
            // stickies shouldn't be timed
            if (Current != null && Current.Sticky && StickyPinsRotation)
            {
                StopTimer();
                return;
            }

            // next queued item or clear
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

            // stickies pin
            if (Current.Sticky && StickyPinsRotation)
            {
                StopTimer();
                return;
            }

            // shrink dwell with more remaining
            int remainingCount = _queue.Count + 1;
            double seconds = Math.Max(MinSecondsPerItem, SecondsPerItem - (remainingCount - 1));

            _timer.Interval = TimeSpan.FromSeconds(seconds);

            // restart to apply new interval
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

        private void SetCurrent(StatusMessage? msg)
        {
            if (!ReferenceEquals(Current, msg))
            {
                Current = msg;
                CurrentChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
