using SharpHook;
using SharpHook.Data;
using SharpHook.Native;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace DSAMVVM.Core.Services
{
    public class QuickSearchService
    {
        private readonly IGlobalHook _hook;
        private readonly IEventSimulator _simulator;

        private long _lastPressTime = 0;
        private bool _isCtrlDown = false; // Tracks physical key state to prevent auto-repeat ghosting
        private const int DoubleTapThresholdMs = 400; // Slightly increased for a more natural feel

        public event EventHandler<string>? QuickSearchTriggered;

        public QuickSearchService()
        {
            _hook = new TaskPoolGlobalHook(); // TaskPool is generally safer for UI apps than EventLoop
            _simulator = new EventSimulator();

            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;
        }

        public void Start() => _hook.RunAsync(); // RunAsync is non-blocking

        public void Stop() => _hook.Dispose();

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if (e.Data.KeyCode == KeyCode.VcLeftControl)
            {
                if (_isCtrlDown) return; // Ignore OS auto-repeat if the key is just being held down
                _isCtrlDown = true;

                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long diff = currentTime - _lastPressTime;

                // 50ms debounce prevents mechanical switch bouncing from counting as a tap
                if (diff > 50 && diff < DoubleTapThresholdMs)
                {
                    _lastPressTime = 0; // Reset sequence
                    ExecuteCapture();
                }
                else
                {
                    _lastPressTime = currentTime;
                }
            }
        }

        private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
        {
            if (e.Data.KeyCode == KeyCode.VcLeftControl)
            {
                _isCtrlDown = false;
            }
        }

        private void ExecuteCapture()
        {
            // Use BeginInvoke so we don't block the global hook while waiting for the clipboard
            Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                string capturedText = string.Empty;

                try
                {
                    // 1. Fire the stealth copy
                    _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                    _simulator.SimulateKeyPress(KeyCode.VcC);
                    _simulator.SimulateKeyRelease(KeyCode.VcC);
                    _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);

                    // 2. Wait for OS to populate clipboard (150ms is the sweet spot)
                    await Task.Delay(150);

                    // 3. Safely attempt to read the clipboard
                    if (Clipboard.ContainsText())
                    {
                        capturedText = Clipboard.GetText();
                    }
                }
                catch (Exception ex)
                {
                    // If the clipboard is locked by another process, we swallow the exception.
                    // We still want the overlay to open, it will just have an empty search box.
                    System.Diagnostics.Debug.WriteLine($"Clipboard access failed: {ex.Message}");
                }
                finally
                {
                    // 4. GUARANTEE the event fires to summon the overlay, even if text is empty
                    QuickSearchTriggered?.Invoke(this, capturedText);
                }
            }));
        }
    }
}