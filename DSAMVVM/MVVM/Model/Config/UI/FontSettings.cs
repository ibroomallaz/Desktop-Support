namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class FontSettings
    {
        public double DefaultSize { get; set; } = 12.0;
        public bool ViewFontSizeOverride { get; set; }
        public void Clamp() => DefaultSize = UiLimits.ClampFontSize(DefaultSize);
    }

    public sealed class ViewFontSetting
    {
        public double FontSize { get; set; } = 12.0;
        public void Clamp() => FontSize = UiLimits.ClampFontSize(FontSize);
    }

    public static class UiLimits
    {
        public const double MinFontSize = 9.0;
        public const double MaxFontSize = 18.0;
        public const double DefaultFontSize = 12.0;
        public static double ClampFontSize(double size) => Math.Clamp(size, MinFontSize, MaxFontSize);
    }
}
