using DSAMVVM.MVVM.Model.Schemas;

namespace DSAMVVM.MVVM.Model.Config
{
    public sealed class AppSettings
    {
        public SettingsMeta Meta { get; set; } = new();
        public Paths.Paths Paths { get; set; } = new();
        public UI.Ui Ui { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
        public UpdateSettings Updates { get; set; } = new();
        public QuickSearchSettings QuickSearch { get; set; } = new();

        public void ApplyDefaultsAndClamp()
        {
            Meta?.Normalize();
            Ui?.NormalizeAll();
            Paths?.NormalizeAll();
            Logging?.Clamp();
            QuickSearch?.Clamp();
        }
    }
}