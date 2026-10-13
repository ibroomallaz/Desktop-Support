using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DSAMVVM.MVVM.View.Helpers
{
    /// <summary>
    /// Manages modern Windows 10/11 DWM (Desktop Window Manager) attributes:
    /// immersive dark mode, native window corner preferences, and system backdrops (Mica / Acrylic).
    /// </summary>
    public static partial class WindowBackdropHelper
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        public enum CornerPreference
        {
            Default = 0,
            DoNotRound = 1,
            Round = 2,
            RoundSmall = 3
        }

        public enum BackdropType
        {
            Auto = 0,
            None = 1,
            MainWindow = 2, // Mica
            TransientWindow = 3, // Acrylic
            TabbedWindow = 4 // Mica Alt
        }

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        /// <summary>
        /// Applies modern Windows 10/11 DWM attributes to a window's HWND upon SourceInitialized.
        /// </summary>
        public static void ApplyModernAttributes(
            Window window,
            bool darkMode = true,
            CornerPreference corner = CornerPreference.Round,
            BackdropType backdrop = BackdropType.None)
        {
            if (Environment.OSVersion.Version.Major < 10) return;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            // 1. Immersive dark mode (Windows 10 1809+ and Windows 11)
            int isDark = darkMode ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDark, sizeof(int));

            // Windows 11 specific features (Build 22000+)
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                // 2. Corner rounding
                int cornerVal = (int)corner;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerVal, sizeof(int));

                // 3. System Backdrop (Windows 11 22H2+ / Build 22621+)
                if (Environment.OSVersion.Version.Build >= 22621 && backdrop != BackdropType.None)
                {
                    int backdropVal = (int)backdrop;
                    DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropVal, sizeof(int));
                }
            }
        }
    }
}
