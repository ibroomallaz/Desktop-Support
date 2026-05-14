using SharpHook;
using SharpHook.Data;
using System.Windows;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.Core.Interfaces;

namespace DSAMVVM.Core.Services
{
    public class QuickSearchService : IQuickSearchService
    {
        private readonly TaskPoolGlobalHook _hook;
        private readonly EventSimulator _simulator;

        private QuickSearchSettings _settings = new();
        private long _lastPressTime = 0;
        private bool _isTriggerKeyDown = false;

        public event EventHandler<string>? QuickSearchTriggered;

        public QuickSearchService()
        {
            _hook = new TaskPoolGlobalHook();
            _simulator = new EventSimulator();

            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;
        }

        // Method to accept on startup and when Apply is clicked in settings
        public void Configure(QuickSearchSettings settings)
        {
            _settings = settings;

            if (_settings.Enabled && !_hook.IsRunning)
            {
                Start();
            }
            //Commenting out stopping behavior. Causes ObjectDisposedException when the user tries to re-enable
            // Hook is lightweight and doesn't consume much resources when running
            /*else if (!_settings.Enabled && _hook.IsRunning)
                Stop();
        */
            }

        public void Start() => _hook.RunAsync();

        public void Stop() => _hook.Dispose();

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if (!_settings.Enabled) return;

            if (e.Data.KeyCode == _settings.ModifierKeyCode)
            {
                if (_isTriggerKeyDown) return; // Prevent holding the key down from triggering it
                _isTriggerKeyDown = true;

                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long diff = currentTime - _lastPressTime;

                // Use the configured double-tap threshold (>50ms prevents mechanical switch bounce)
                if (diff > 50 && diff < _settings.DoubleTapThresholdMs)
                {
                    _lastPressTime = 0;
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
            if (e.Data.KeyCode == _settings.ModifierKeyCode)
            {
                _isTriggerKeyDown = false;
            }
        }

        private void ExecuteCapture()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                string capturedText = string.Empty;

                try
                {
                    // 1. Explicitly release the user's custom trigger key so it doesn't interfere
                    _simulator.SimulateKeyRelease(_settings.ModifierKeyCode);

                    // 2. ALWAYS fire a hardcoded LeftControl + C to copy the text to the clipboard
                    _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                    _simulator.SimulateKeyPress(KeyCode.VcC);
                    _simulator.SimulateKeyRelease(KeyCode.VcC);
                    _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);

                    await Task.Delay(150); // Wait for Windows to write to clipboard

                    if (Clipboard.ContainsText())
                    {
                        capturedText = Clipboard.GetText();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Clipboard access failed: {ex.Message}");
                }
                finally
                {
                    QuickSearchTriggered?.Invoke(this, capturedText);
                }
            }));
        }
    }
}