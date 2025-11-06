using System.Windows;

namespace DSAMVVM.MVVM.View
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string text)
        {
            if (!IsLoaded) return;
            if (Dispatcher.CheckAccess())
                StatusText.Text = text;
            else
                Dispatcher.BeginInvoke(new Action(() => StatusText.Text = text));
        }
    }
}
