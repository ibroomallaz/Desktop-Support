using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace DSAMVVM.MVVM.View
{
    public partial class LinksView : UserControl
    {
        public LinksView()
        {
            InitializeComponent();
        }

        // open links -> default browser
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
