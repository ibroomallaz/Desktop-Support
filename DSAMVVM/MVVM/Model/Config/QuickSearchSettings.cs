namespace DSAMVVM.MVVM.Model.Config
{
    public sealed class QuickSearchSettings
    {
        public bool Enabled { get; set; } = true;
        public ushort TriggerKeyCode { get; set; } = 0x001D;
        public int DoubleTapThresholdMs { get; set; } = 350;

        public void Clamp()
        {
            DoubleTapThresholdMs = Math.Clamp(DoubleTapThresholdMs, 100, 1000);
        }
    }
}