namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class TrayUiSettings
    {
        public bool EnableTrayIcon { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool CloseToTray { get; set; }

        public void Normalize()
        {
            if (!EnableTrayIcon) { MinimizeToTray = false; CloseToTray = false; }
        }
    }
}