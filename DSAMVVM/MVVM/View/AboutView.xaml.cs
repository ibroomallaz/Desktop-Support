using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using DSAMVVM.MVVM.ViewModel;

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

        // Optional passthroughs if XAML uses Click= handlers; otherwise bind to commands in XAML.
        private void OnOpenGitHubClick(object sender, RoutedEventArgs e) => _vm.OpenGitHubCommand.Execute(null);
        private void OnOpenSharePointClick(object sender, RoutedEventArgs e) => _vm.OpenSharePointCommand.Execute(null);
        private void OnCheckVersionClick(object sender, RoutedEventArgs e) => _vm.CheckVersionCommand.Execute(null);

        private static void TryOpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open: {url}\n\n{ex.Message}", "Open Link",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
