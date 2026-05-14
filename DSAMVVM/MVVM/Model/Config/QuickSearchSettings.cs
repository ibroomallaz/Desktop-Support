using SharpHook.Data;

namespace DSAMVVM.MVVM.Model.Config
{
    public sealed class QuickSearchSettings
    {
        public bool Enabled { get; set; } = true;

        // Default to Left Control
        public KeyCode ModifierKeyCode { get; set; } = KeyCode.VcLeftControl;

        public int DoubleTapThresholdMs { get; set; } = 350;

        public void Clamp()
        {
            DoubleTapThresholdMs = Math.Clamp(DoubleTapThresholdMs, 100, 1000);
        }
    }
}