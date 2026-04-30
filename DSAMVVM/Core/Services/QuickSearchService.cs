using SharpHook;
using SharpHook.Data;
using System.Windows;

namespace DSAMVVM.Core.Services
{
    public class QuickSearchService
    {
        private readonly IGlobalHook _hook;
        private readonly IEventSimulator _simulator;
        private long _lastPressTime = 0;
        private const int DoubleTapThresholdMs = 350; // Hardcoded threshold

        public event EventHandler<string>? QuickSearchTriggered;

        public QuickSearchService()
        {
            _hook = new EventLoopGlobalHook();
            _simulator = new EventSimulator();
            _hook.KeyPressed += OnKeyPressed;
        }

        public void Start() => Task.Run(() => _hook.Run());

        public void Stop() => _hook.Dispose();

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            // Hardcoded to Left Control
            if (e.Data.KeyCode == KeyCode.VcLeftControl)
            {
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (currentTime - _lastPressTime < DoubleTapThresholdMs)
                {
                    ExecuteCapture();
                    _lastPressTime = 0;
                }
                else
                {
                    _lastPressTime = currentTime;
                }
            }
        }

        private void ExecuteCapture()
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                var oldData = Clipboard.GetDataObject();

                // Stealth Copy: Ctrl + C
                _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                _simulator.SimulateKeyPress(KeyCode.VcC);
                _simulator.SimulateKeyRelease(KeyCode.VcC);
                _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);

                await Task.Delay(100); // Give OS time to populate clipboard

                string capturedText = Clipboard.GetText();

                // Restore previous clipboard state
                if (oldData != null) Clipboard.SetDataObject(oldData);

                QuickSearchTriggered?.Invoke(this, capturedText);
            });
        }
    }
}