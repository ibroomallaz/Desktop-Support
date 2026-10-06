using System;
using System.Windows;
using System.Windows.Media.Animation;
using DSAMVVM.MVVM.Model;

namespace DSAMVVM.MVVM.View
{
    public partial class SplashWindow
    {
        public SplashWindow()
        {
            InitializeComponent();

            try
            {
                VersionText.Text = $"v{Globals.g_AppVersion}";
            }
            catch
            {
                VersionText.Text = string.Empty;
            }
        }

        public void UpdateStatus(string text)
        {
            if (!IsLoaded) return;
            if (Dispatcher.CheckAccess())
                StatusText.Text = text;
            else
                Dispatcher.BeginInvoke(new Action(() => StatusText.Text = text));
        }

        public void FadeOutAndClose(int durationMs = 160)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => FadeOutAndClose(durationMs)));
                return;
            }

            if (!IsLoaded)
            {
                try { Close(); } catch { /* ignored */ }
                return;
            }

            try
            {
                var anim = new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(durationMs)))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                anim.Completed += (_, _) =>
                {
                    try { Close(); }
                    catch { /* ignored */ }
                };

                BeginAnimation(OpacityProperty, anim);
            }
            catch
            {
                try { Close(); }
                catch { /* ignored */ }
            }
        }
    }
}
