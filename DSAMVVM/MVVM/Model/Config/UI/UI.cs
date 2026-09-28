namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class Ui
    {
        public FontSettings Font { get; set; } = new();
        public Dictionary<string, ViewFontSetting> ViewFontSizes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public SearchSettings Search { get; set; } = new();
        public LinksUiSettings Links { get; set; } = new();
        public TrayUiSettings Tray { get; set; } = new();
        public ServiceMeowUiSettings ServiceMeow { get; set; } = new();
        public HomeShortcutsSettings Shortcuts { get; set; } = new();

        //Admin panel settings
        public bool HasUnlockedAdmin { get; set; }
        public bool ShowAdminView { get; set; }

        public void NormalizeAll()
        {
            Search.Clamp();
            Font.Clamp();
            Tray.Normalize();
            Links.Normalize();
            ServiceMeow.Normalize();
            Shortcuts.Normalize();

            if (!ReferenceEquals(ViewFontSizes.Comparer, StringComparer.OrdinalIgnoreCase))
            {
                ViewFontSizes = new Dictionary<string, ViewFontSetting>(ViewFontSizes, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var kvp in ViewFontSizes) kvp.Value?.Clamp();
        }
    }
}
