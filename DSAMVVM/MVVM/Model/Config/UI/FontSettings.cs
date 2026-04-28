namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class FontSettings
    {
        public double DefaultSize { get; set; } = 14.0;
        public bool ViewFontSizeOverride { get; set; } = false;
        public void Clamp() => DefaultSize = UiLimits.ClampFontSize(DefaultSize);
    }

    public sealed class ViewFontSetting
    {
        public double FontSize { get; set; }
        public void Clamp() => FontSize = UiLimits.ClampFontSize(FontSize);
    }

    public static class UiLimits
    {
        public const double MinFontSize = 8.0;
        public const double MaxFontSize = 24.0;
        public static double ClampFontSize(double size) => Math.Clamp(size, MinFontSize, MaxFontSize);
    }
}