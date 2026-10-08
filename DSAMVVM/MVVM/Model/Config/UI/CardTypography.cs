namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class CardTypography
    {
        public double BaseSize { get; }
        public double TitleSize { get; }
        public double BodySize { get; }
        public double CaptionSize { get; }
        public double BadgeSize { get; }
        public double IconSize { get; }
        public double NotesSize { get; }

        public CardTypography(double baseSize)
        {
            BaseSize = UiLimits.ClampFontSize(baseSize);
            TitleSize = Math.Round(BaseSize * 1.25, 1);
            BodySize = Math.Round(BaseSize * 1.0, 1);
            CaptionSize = Math.Round(BaseSize * 0.88, 1);
            BadgeSize = Math.Round(Math.Clamp(BaseSize * 0.82, 9.0, 11.5), 1);
            IconSize = Math.Round(Math.Clamp(BaseSize * 1.15, 12.0, 18.0), 1);
            NotesSize = Math.Round(BaseSize * 0.88, 1);
        }

        public static CardTypography FromBase(double baseSize) => new(baseSize);
    }
}
