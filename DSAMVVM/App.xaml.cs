using DSAMVVM.MVVM.Model.utils;
using System.Threading.Tasks;
using System.Windows;

namespace DSAMVVM
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            // Fire-and-forget version check — runs concurrently
            _ = Task.Run(async () =>
            {
                await VersionChecker.VersionCheck();
            });
        }
    }
}
