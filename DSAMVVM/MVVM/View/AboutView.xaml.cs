using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace DSAMVVM.MVVM.View
{
    public partial class AboutView : UserControl
    {
        private readonly AboutViewModel _vm;

        public AboutView()
        {
            InitializeComponent();

            // Resolve VM from DI and wire URL open
            _vm = App.Services.GetRequiredService<AboutViewModel>();
            _vm.OpenUrlRequested += (_, url) => TryOpenUrl(url);

            DataContext = _vm;
        }

        private static void TryOpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open: {url}\n\n{ex.Message}", "Open Link",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}