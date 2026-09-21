namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class Ui
    {
        public FontSettings Font { get; set; } = new();
        public Dictionary<string, ViewFontSetting> ViewFontSizes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public SearchSettings Search { get; set; } = new();
        public LinksUiSettings Links { get; set; } = new();
        public TrayUiSettings Tray { get; set; } = new();
        //Admin panel settings
        public bool HasUnlockedAdmin { get; set; } = false;
        public bool ShowAdminView { get; set; } = false;

        public void NormalizeAll()
        {
            Search?.Clamp();
            Font?.Clamp();
            Tray?.Normalize();
            Links?.Normalize();

            if (ViewFontSizes != null && !ReferenceEquals(ViewFontSizes.Comparer, StringComparer.OrdinalIgnoreCase))
            {
                ViewFontSizes = new Dictionary<string, ViewFontSetting>(ViewFontSizes, StringComparer.OrdinalIgnoreCase);
            }

            if (ViewFontSizes != null)
            {
                foreach (var kvp in ViewFontSizes) kvp.Value?.Clamp();
            }
        }
    }
}