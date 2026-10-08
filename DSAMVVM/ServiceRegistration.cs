using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Services.AD;
using DSAMVVM.Core.Services.Graph;
using DSAMVVM.Core.Services.Infrastructure;
using DSAMVVM.Core.Services.Integrations;
using DSAMVVM.Core.Services.Search;
using DSAMVVM.Core.Services.UI;
using DSAMVVM.Core.Services.Updates;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Services.Status;
using DSAMVVM.MVVM.Services.Updates;
using DSAMVVM.MVVM.ViewModel;
using DSAMVVM.MVVM.ViewModel.Overlays;
using Microsoft.Extensions.DependencyInjection;

namespace DSAMVVM;

public static class ServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Logging & Infrastructure
        services.AddSingleton<IAppLogger>(_ => new FileLogger(Globals.g_LogsDir));
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<IJsonExportService, JsonExportService>();
        services.AddSingleton<INetworkDetectionService, NetworkDetectionService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IApplicationStateService, ApplicationStateService>();
        services.AddSingleton<AppSettings>(_ => App.Settings);

        // Active Directory & Domain
        services.AddSingleton<IADService, ADService>();
        services.AddSingleton<IADDetectionService, ADDetectionService>();

        // Graph & Communications
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<TeamsRoutingService>();

        // Integrations & External APIs
        services.AddSingleton<IDepartmentService, DepartmentService>();
        services.AddSingleton<IServiceMeowService, ServiceMeowService>();
        services.AddSingleton<ILinksService, LinksService>();
        services.AddSingleton<IAdminService, AdminService>();

        // Search
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IQuickSearchService, QuickSearchService>();

        // UI & Presentation Helpers
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IImageCacheService, ImageCacheService>();
        services.AddSingleton<IDeepLinkRoutingService, DeepLinkRoutingService>();
        services.AddSingleton<IOutputTextSettingsProvider>(sp =>
            new OutputTextSettingsProvider(
                sp.GetRequiredService<ISettingsService>(),
                () => App.Settings));

        // Updates
        services.AddSingleton<IUpdaterService, UpdaterService>();
        services.AddSingleton<VersionCheckerUI>();
        services.AddSingleton<IVersionCheckHandler>(sp => sp.GetRequiredService<VersionCheckerUI>());
        services.AddSingleton<VersionUpdateScheduler>();

        // Status
        services.AddSingleton<StatusBus>();

        return services;
    }

    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // Singleton ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<AdminViewModel>();

        // Transient ViewModels
        services.AddTransient<UserViewModel>();
        services.AddTransient<GroupViewModel>();
        services.AddTransient<ComputerViewModel>();
        services.AddTransient<LinksViewModel>();
        services.AddTransient<EntraViewModel>();
        services.AddTransient<QuickSearchOverlayViewModel>();

        // ViewModel Factories
        services.AddTransient<Func<UserViewModel>>(sp => sp.GetRequiredService<UserViewModel>);
        services.AddTransient<Func<GroupViewModel>>(sp => sp.GetRequiredService<GroupViewModel>);
        services.AddTransient<Func<ComputerViewModel>>(sp => sp.GetRequiredService<ComputerViewModel>);
        services.AddTransient<Func<LinksViewModel>>(sp => sp.GetRequiredService<LinksViewModel>);
        services.AddTransient<Func<AdminViewModel>>(sp => sp.GetRequiredService<AdminViewModel>);

        // HomeViewModel Factory Setup
        services.AddSingleton<HomeViewModel>(sp =>
            new HomeViewModel(
                openUser: q =>
                {
                    var main = sp.GetRequiredService<MainViewModel>();
                    main.SelectedView = AppView.User;
                    if (string.IsNullOrWhiteSpace(q)) return;
                    main.SearchQuery = q;
                    main.ExecuteSearchCommand.Execute(null);
                },
                openComputer: q =>
                {
                    var main = sp.GetRequiredService<MainViewModel>();
                    main.SelectedView = AppView.Computer;
                    if (string.IsNullOrWhiteSpace(q)) return;
                    main.SearchQuery = q;
                    main.ExecuteSearchCommand.Execute(null);
                },
                goGroups: () => sp.GetRequiredService<MainViewModel>().SelectedView = AppView.Group,
                goEntra: () => sp.GetRequiredService<MainViewModel>().SelectedView = AppView.Entra,
                goLinks: () => sp.GetRequiredService<MainViewModel>().SelectedView = AppView.Links,
                goAbout: () => sp.GetRequiredService<MainViewModel>().SelectedView = AppView.About,
                networkService: sp.GetRequiredService<INetworkDetectionService>(),
                adDetectionService: sp.GetRequiredService<IADDetectionService>(),
                authService: sp.GetRequiredService<IAuthenticationService>(),
                searchService: sp.GetRequiredService<ISearchService>(),
                linkRouter: sp.GetRequiredService<IDeepLinkRoutingService>(),
                imageCacheService: sp.GetRequiredService<IImageCacheService>(),
                settingsService: sp.GetRequiredService<ISettingsService>(),
                serviceMeowService: sp.GetRequiredService<IServiceMeowService>()
            )
        );

        return services;
    }
}
