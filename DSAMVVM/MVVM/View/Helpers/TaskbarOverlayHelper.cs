using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DSAMVVM.MVVM.View.Helpers
{
    /// <summary>
    /// Generates crisp, high-visibility solid dot overlay badges for the Windows Taskbar button (TaskbarItemInfo.Overlay).
    /// Modeled after modern status indicators (e.g., Teams, Slack, Discord) for zero blur and maximum clarity at any DPI.
    /// </summary>
    public static class TaskbarOverlayHelper
    {
        private static ImageSource? _offCampusBadge;
        private static ImageSource? _disconnectedBadge;
        private static readonly Lock _lock = new();

        /// <summary>
        /// Solid amber dot badge for Off-Campus network state.
        /// </summary>
        public static ImageSource OffCampusBadge
        {
            get
            {
                if (_offCampusBadge != null) return _offCampusBadge;
                lock (_lock)
                {
                    return _offCampusBadge ??= CreateSolidDotBadge(
                        dotColor: Color.FromRgb(0xF5, 0x9E, 0x0B)); // Vibrant Amber (#F59E0B)
                }
            }
        }

        /// <summary>
        /// Solid slate-gray dot badge for Disconnected network state.
        /// </summary>
        public static ImageSource DisconnectedBadge
        {
            get
            {
                if (_disconnectedBadge != null) return _disconnectedBadge;
                lock (_lock)
                {
                    return _disconnectedBadge ??= CreateSolidDotBadge(
                        dotColor: Color.FromRgb(0x6B, 0x72, 0x80)); // Neutral Slate Gray (#6B7280)
                }
            }
        }

        private static ImageSource CreateSolidDotBadge(Color dotColor)
        {
            const int size = 32;
            const double center = size / 2.0;
            const double radius = 13.5;

            var visual = new DrawingVisual();

            using (var dc = visual.RenderOpen())
            {
                // Solid dot fill
                var fillBrush = new SolidColorBrush(dotColor);
                fillBrush.Freeze();
                dc.DrawEllipse(fillBrush, null, new Point(center, center), radius, radius);

                // High-contrast outer stroke to ensure separation from the underlying app icon and taskbar theme
                var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 2.0);
                borderPen.Freeze();
                dc.DrawEllipse(null, borderPen, new Point(center, center), radius, radius);
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }
    }
}
