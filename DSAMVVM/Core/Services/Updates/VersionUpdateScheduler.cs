using DSAMVVM.Core.Interfaces;
using Microsoft.Win32;

namespace DSAMVVM.Core.Services.Updates
{
    // Periodic + resume scheduler (UI-neutral, lean: single-flight, jitter, backoff, resume debounce)
    public sealed class VersionUpdateScheduler : IDisposable
    {
        private readonly IVersionCheckHandler _handler;
        private readonly SemaphoreSlim _singleFlight = new(1, 1); // -> prevent overlap
        private readonly Lock _gate = new();                    // -> swap timer/cts safely

        private CancellationTokenSource? _cts;
        private PeriodicTimer? _timer;

        private TimeSpan _baseInterval = TimeSpan.FromHours(4);
        private int _backoffPow;                                  // -> 0=base, 1=x2 (cap)
        private bool _resumeCheckPending;
        private DateTime _lastRunUtc;                              // -> resume debounce

        private static readonly TimeSpan MaxInterval = TimeSpan.FromHours(12);
        private static readonly TimeSpan ResumeDebounce = TimeSpan.FromHours(2); // -> skip resume if checked recently

        public VersionUpdateScheduler(IVersionCheckHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        public void Start(TimeSpan interval, bool runImmediately = false)
        {
            Stop();
            _baseInterval = interval;

            var cts = new CancellationTokenSource();
            var timer = new PeriodicTimer(WithJitter(CurrentInterval()));

            lock (_gate)
            {
                _cts = cts;
                _timer = timer;
            }

            _ = Task.Run(async () =>
            {
                if (runImmediately)
                {
                    await SafeCheckAsync().ConfigureAwait(false);
                    ResetTimerWith(CurrentInterval());
                }

                while (await GetTimer().WaitForNextTickAsync(GetToken()).ConfigureAwait(false))
                {
                    await SafeCheckAsync().ConfigureAwait(false);
                    ResetTimerWith(CurrentInterval());
                }
            }, cts.Token);
        }

        public void Stop()
        {
            lock (_gate)
            {
                try { _cts?.Cancel(); } catch { }
                _timer?.Dispose();
                _cts?.Dispose();
                _timer = null;
                _cts = null;
            }
        }

        private async void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume || _resumeCheckPending) return;

            // -> debounce: skip if we ran within the last 2h
            if (DateTime.UtcNow - _lastRunUtc < ResumeDebounce) return;

            _resumeCheckPending = true;
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false); // -> allow Wi-Fi/VPN to settle
                await SafeCheckAsync().ConfigureAwait(false);
                ResetTimerWith(CurrentInterval());
            }
            finally { _resumeCheckPending = false; }
        }

        private async Task SafeCheckAsync()
        {
            if (!await _singleFlight.WaitAsync(0).ConfigureAwait(false)) return;

            try
            {
                await _handler.EnforceRequiredAsync().ConfigureAwait(false);
                await _handler.CheckAsync().ConfigureAwait(false);
                _backoffPow = 0;                  // -> success resets backoff
                _lastRunUtc = DateTime.UtcNow;    // -> record last successful check
            }
            catch
            {
                if (_backoffPow < 1) _backoffPow++; // -> 6h → 12h (cap)
            }
            finally
            {
                _singleFlight.Release();
            }
        }

        private TimeSpan CurrentInterval()
        {
            var scaled = TimeSpan.FromTicks(_baseInterval.Ticks << _backoffPow);
            return scaled <= MaxInterval ? scaled : MaxInterval;
        }

        private static TimeSpan WithJitter(TimeSpan baseInterval)
        {
            var min = Random.Shared.Next(-30, 31); // -> ±30 minutes
            return baseInterval + TimeSpan.FromMinutes(min);
        }

        private void ResetTimerWith(TimeSpan interval)
        {
            lock (_gate)
            {
                if (_cts == null) return;
                _timer?.Dispose();
                _timer = new PeriodicTimer(WithJitter(interval));
            }
        }

        private PeriodicTimer GetTimer()
        {
            lock (_gate) return _timer ?? new PeriodicTimer(TimeSpan.FromDays(365)); // -> inert fallback
        }

        private CancellationToken GetToken()
        {
            lock (_gate) return _cts?.Token ?? new CancellationToken(true);
        }

        public void Dispose()
        {
            Stop();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _singleFlight.Dispose();
        }
    }
}