namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class TrayUiSettings
    {
        public bool EnableTrayIcon { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool CloseToTray { get; set; } = false;

        public void Normalize()
        {
            if (!EnableTrayIcon) { MinimizeToTray = false; CloseToTray = false; }
        }
    }
}