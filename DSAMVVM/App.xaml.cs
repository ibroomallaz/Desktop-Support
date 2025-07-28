using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.utils;
using DSAMVVM.MVVM.ViewModel;
using System.Threading.Tasks;
using System.Windows;

namespace DSAMVVM
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainVM = new MainViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = mainVM
            };

            MainWindow = mainWindow;
            mainWindow.Show();

            _ = Task.Run(async () =>
            {
                var versionChecker = new VersionCheckerUI(mainVM.StatusBar);
                await versionChecker.CheckAsync();
            });
        }
    }
}
