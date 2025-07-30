using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Services;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace DSAMVVM
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureServices();

            var mainVM = _serviceProvider!.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainVM
            };

            MainWindow = mainWindow;
            mainWindow.Show();

            _ = Task.Run(async () =>
            {
                var versionChecker = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                await versionChecker.CheckAsync();
            });
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core shared services
            services.AddSingleton<StatusBarViewModel>();
            services.AddSingleton<IStatusReporter>(sp => sp.GetRequiredService<StatusBarViewModel>());
            services.AddSingleton<IDepartmentService, DepartmentService>();
            services.AddSingleton<IADService, ADService>();
            services.AddSingleton<ILinksService, LinksService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<UserViewModel>();
            services.AddTransient<GroupViewModel>();
            services.AddTransient<ComputerViewModel>();
            services.AddTransient<LinksViewModel>();

            // ViewModel factories for MainViewModel constructor
            services.AddTransient<Func<UserViewModel>>(sp => () => sp.GetRequiredService<UserViewModel>());
            services.AddTransient<Func<GroupViewModel>>(sp => () => sp.GetRequiredService<GroupViewModel>());
            services.AddTransient<Func<ComputerViewModel>>(sp => () => sp.GetRequiredService<ComputerViewModel>());

            // Tools and utilities
            services.AddTransient<VersionCheckerUI>();

            _serviceProvider = services.BuildServiceProvider();
        }
    }
}
