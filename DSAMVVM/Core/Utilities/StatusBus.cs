using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using DSAMVVM.MVVM.Model; // StatusMessage

namespace DSAMVVM.Core.Utilities
{
    /* Central stream of status messages:
     - Replace existing messages by key (no stacking)
     - Exposes a single "Current" item for single-line bar
     - Rotates when multiple messages exist
     - If any message is Sticky, rotation pins to the best sticky */
    public sealed class StatusBus
    {
        // Full list (history / debugging / future panes)
        public ObservableCollection<StatusMessage> Messages { get; } = new();

        // Single item the status bar binds to
        public StatusMessage? Current { get; private set; }
        public event EventHandler? CurrentChanged;

        // Replace-by-key lookup (latest per key)
        private readonly Dictionary<string, StatusMessage> _byKey = new(StringComparer.OrdinalIgnoreCase);

        // Timestamps for recency
        private readonly Dictionary<StatusMessage, DateTime> _ts = new();

        // Rotation config
        public bool AutoRotateEnabled { get; set; } = true;
        public double BaseSecondsPerItem { get; set; } = 6.0;
        public double MinSecondsPerItem { get; set; } = 2.0;

        // Sticky pins rotation by default
        public bool StickyPinsRotation { get; set; } = true;

        // Rotation state
        private readonly DispatcherTimer _rotateTimer;
        private List<StatusMessage> _order = new();
        private int _cursor = 0;
        private string? _currentKeySnapshot;

        public StatusBus()
        {
            _rotateTimer = new DispatcherTimer { IsEnabled = false };
            _rotateTimer.Tick += (_, __) => AdvanceRotation();
        }

        // Publish a message
        public void Report(StatusMessage msg)
        {
            void OnUi()
            {
                _ts[msg] = DateTime.UtcNow;

                // Replace in-place if keyed and already present
                if (!string.IsNullOrEmpty(msg.Key) && _byKey.TryGetValue(msg.Key!, out var existing))
                {
                    int idx = Messages.IndexOf(existing);
                    if (idx >= 0) Messages[idx] = msg;
                    _byKey[msg.Key!] = msg;
                }
                else
                {
                    Messages.Add(msg);
                    if (!string.IsNullOrEmpty(msg.Key))
                        _byKey[msg.Key!] = msg;
                }

                RecomputeOrderAndRotation();
            }

            var d = Application.Current?.Dispatcher;
            if (d != null && !d.CheckAccess()) d.Invoke(OnUi); else OnUi();
        }

        // Remove a message by key
        public void RemoveByKey(string key)
        {
            void OnUi()
            {
                if (_byKey.TryGetValue(key, out var msg))
                {
                    Messages.Remove(msg);
                    _byKey.Remove(key);
                    _ts.Remove(msg);
                    if (ReferenceEquals(Current, msg)) _currentKeySnapshot = null;
                }
                RecomputeOrderAndRotation();
            }

            var d = Application.Current?.Dispatcher;
            if (d != null && !d.CheckAccess()) d.Invoke(OnUi); else OnUi();
        }

        // Clear everything and stop rotation
        public void Clear()
        {
            void OnUi()
            {
                _rotateTimer.Stop();
                Messages.Clear();
                _byKey.Clear();
                _ts.Clear();
                _order.Clear();
                _cursor = 0;
                SetCurrent(null);
            }

            var d = Application.Current?.Dispatcher;
            if (d != null && !d.CheckAccess()) d.Invoke(OnUi); else OnUi();
        }

        // Rebuild order and decide whether to rotate or pin
        private void RecomputeOrderAndRotation()
        {
            // Remember which key is showing so we can keep place if possible
            _currentKeySnapshot = Current?.Key;

            // Sort: priority dec, sticky first, then most recent
            _order = Messages
                .OrderByDescending(m => m.Priority)
                .ThenByDescending(m => m.Sticky)
                .ThenByDescending(m => _ts.TryGetValue(m, out var t) ? t : DateTime.MinValue)
                .ToList();

            // If sticky pins are enabled and any sticky exists, pin to the highest
            if (StickyPinsRotation && _order.Any(m => m.Sticky))
            {
                _rotateTimer.Stop();
                var topSticky = _order.First(m => m.Sticky);
                SetCurrent(topSticky);
                return;
            }

            // Position cursor at the current item (by key) if it still exists
            if (_currentKeySnapshot != null)
            {
                var idx = _order.FindIndex(m =>
                    string.Equals(m.Key, _currentKeySnapshot, StringComparison.OrdinalIgnoreCase));
                _cursor = idx >= 0 ? idx : 0;
            }
            else
            {
                _cursor = 0;
            }

            // If one or fewer items (or rotation disabled), stop rotating and show it
            if (!AutoRotateEnabled || _order.Count <= 1)
            {
                _rotateTimer.Stop();
                SetCurrent(_order.FirstOrDefault());
                return;
            }

            // Multiple items, no stickies: start/continue rotation
            _rotateTimer.Interval = ComputeInterval(_order.Count);
            if (!_rotateTimer.IsEnabled) _rotateTimer.Start();

            SetCurrent(_order[Math.Clamp(_cursor, 0, _order.Count - 1)]);
        }

        // Move to next item in rotation
        private void AdvanceRotation()
        {
            // If a sticky showed up while we were rotating, pin immediately
            if (StickyPinsRotation && _order.Any(m => m.Sticky))
            {
                _rotateTimer.Stop();
                SetCurrent(_order.First(m => m.Sticky));
                return;
            }

            if (_order.Count == 0)
            {
                _rotateTimer.Stop();
                SetCurrent(null);
                return;
            }

            _cursor = (_cursor + 1) % _order.Count;
            SetCurrent(_order[_cursor]);
        }

        // Shorten per-message time as count grows
        private TimeSpan ComputeInterval(int count)
        {
            double seconds = Math.Max(MinSecondsPerItem, BaseSecondsPerItem - (count - 1));
            return TimeSpan.FromSeconds(seconds);
        }

        // Assign Current and notify UI
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
