using System.IO;
using System.Windows;
using DSA.Tests.Infrastructure;
using DSAMVVM;
using DSAMVVM.Core.Interfaces.Infrastructure;
using DSAMVVM.Core.Interfaces.Integrations;
using DSAMVVM.Core.IO;
using DSAMVVM.Core.Services.Infrastructure;
using DSAMVVM.Core.Services.Integrations;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Model.Data;
using DSAMVVM.MVVM.View;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DSA.Tests.LoadTime;

[TestCaseOrderer("DSA.Tests.Infrastructure.AlphabeticalOrderer", "DSA.Tests")]
[Collection("SequentialLoadBenchmarks")]
public class StartupLoadTimeTests(ITestOutputHelper output)
{
    [Fact]
    public void T01_DiContainer_And_MainViewModelGraph_LoadTime()
    {
        var result = LoadBenchmarkRunner.RunOnStaThread(() =>
        {
            TestAppHarness.EnsureWpfAppInitialized();

            return LoadBenchmarkRunner.Measure(
                scenarioName: "1. DI Container Build + MainViewModel Graph Resolution",
                action: () =>
                {
                    var provider = TestAppHarness.BuildTestServiceProvider();
                    TestAppHarness.SetAppServices(provider);

                    var mainVm = provider.GetRequiredService<MainViewModel>();
                    mainVm.BootstrapInitialView();

                    Assert.NotNull(mainVm.HomeVM);
                    Assert.NotNull(mainVm.UserVM);
                    Assert.NotNull(mainVm.ComputerVM);
                    Assert.NotNull(mainVm.GroupVM);
                    Assert.NotNull(mainVm.LinksVM);
                    Assert.NotNull(mainVm.SettingsVM);
                    Assert.NotNull(mainVm.AdminVM);
                    Assert.NotEmpty(mainVm.NavItems);
                },
                warmIterations: 80,
                output: output);
        });

        Assert.True(result.WarmMedianMs >= 0);
    }

    [Fact]
    public async Task T02_SettingsService_LoadAndClamp_LoadTime()
    {
        LoadBenchmarkRunner.RunOnStaThread(TestAppHarness.EnsureWpfAppInitialized);

        string tempDir = Path.Combine(Path.GetTempPath(), $"DSA_SettingsPerf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string settingsPath = Path.Combine(tempDir, "settings.json");

        try
        {
            await File.WriteAllTextAsync(settingsPath, TestAppHarness.SyntheticSettingsJson);
            var settingsService = new SettingsService();

            var result = await LoadBenchmarkRunner.MeasureAsync(
                scenarioName: "2. SettingsService LoadAsync + ApplyDefaultsAndClamp",
                asyncAction: async () =>
                {
                    var loaded = await settingsService.LoadAsync(settingsPath);
                    Assert.NotNull(loaded);
                    Assert.Equal(6, loaded.Ui.Shortcuts.Items.Count);
                },
                warmIterations: 80,
                output: output);

            Assert.True(result.WarmMedianMs >= 0);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task T03_DepartmentService_PreCacheAndLookup_LoadTime()
    {
        LoadBenchmarkRunner.RunOnStaThread(TestAppHarness.EnsureWpfAppInitialized);

        string tempDir = Path.Combine(Path.GetTempPath(), $"DSA_DeptPerf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string deptFile = Path.Combine(tempDir, "departments.json");

        try
        {
            await File.WriteAllTextAsync(deptFile, TestAppHarness.SyntheticDepartmentsJson);

            // Configure custom local file source so we measure pure file I/O + JSON deserialization + dictionary indexing
            App.Settings.Paths.DepartmentData.UseCustomSource = true;
            App.Settings.Paths.DepartmentData.Source = "File";
            App.Settings.Paths.DepartmentData.Uri = deptFile;

            var http = new TestAppHarness.FakeHttpService();
            var appState = new ApplicationStateService();
            var deptService = new DepartmentService(http, appState);

            var result = await LoadBenchmarkRunner.MeasureAsync(
                scenarioName: "3. DepartmentService Load + Index (500 Depts, 35 Teams)",
                asyncAction: async () =>
                {
                    await deptService.ReloadDataAsync();
                    var dept = await deptService.GetDepartmentAsync("0250");
                    var divTeams = await deptService.GetTeamsByDivisionAsync("DV05");

                    Assert.NotNull(dept);
                    Assert.NotEmpty(divTeams);
                },
                warmIterations: 60,
                output: output);

            Assert.True(result.WarmMedianMs >= 0);
        }
        finally
        {
            App.Settings.Paths.DepartmentData.UseCustomSource = false;
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task T04_LinksAndServiceMeow_Warmup_LoadTime()
    {
        LoadBenchmarkRunner.RunOnStaThread(TestAppHarness.EnsureWpfAppInitialized);

        string tempDir = Path.Combine(Path.GetTempPath(), $"DSA_FeedPerf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string linksFile = Path.Combine(tempDir, "links.json");
        string meowFile = Path.Combine(tempDir, "servicemeow.json");

        try
        {
            await File.WriteAllTextAsync(linksFile, TestAppHarness.SyntheticLinksJson);
            await File.WriteAllTextAsync(meowFile, TestAppHarness.SyntheticServiceMeowJson);

            App.Settings.Paths.LinksData.UseCustomSource = true;
            App.Settings.Paths.LinksData.Source = "File";
            App.Settings.Paths.LinksData.Uri = linksFile;

            App.Settings.Paths.ServiceMeowData.UseCustomSource = true;
            App.Settings.Paths.ServiceMeowData.Source = "File";
            App.Settings.Paths.ServiceMeowData.Uri = meowFile;

            var http = new TestAppHarness.FakeHttpService();
            var settingsService = new TestAppHarness.IsolatedSettingsService();
            var linksService = new LinksService(http);
            var imageCache = App.Services.GetRequiredService<DSAMVVM.Core.Interfaces.UI.IImageCacheService>();
            var meowService = new ServiceMeowService(http, imageCache);

            var result = await LoadBenchmarkRunner.MeasureAsync(
                scenarioName: "4. LinksVM + ServiceMeow Warmup Hydration (350 Links, 80 Pets)",
                asyncAction: async () =>
                {
                    await linksService.ReloadLinksDataAsync();
                    var linksVm = new LinksViewModel(linksService, settingsService);
                    await linksVm.EnsureLoadedAsync();

                    await meowService.ReloadServiceMeowDataAsync();
                    var meowData = meowService.GetCachedData();

                    Assert.Equal(50, linksVm.CommonLinksCount);
                    Assert.Equal(25, linksVm.TeamNames.Count);
                    Assert.NotNull(meowData);
                    Assert.Equal(80, meowData.AllPets.Count);
                },
                warmIterations: 60,
                output: output);

            Assert.True(result.WarmMedianMs >= 0);
        }
        finally
        {
            App.Settings.Paths.LinksData.UseCustomSource = false;
            App.Settings.Paths.ServiceMeowData.UseCustomSource = false;
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task T05_RemoteWithBackUpLoader_CacheSynchronization_LoadTime()
    {
        LoadBenchmarkRunner.RunOnStaThread(TestAppHarness.EnsureWpfAppInitialized);

        string tempDir = Path.Combine(Path.GetTempPath(), $"DSA_CachePerf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string cacheFile = Path.Combine(tempDir, "links_cache.json");

        try
        {
            var http = new TestAppHarness.FakeHttpService();
            var cache = new JsonFileCache<LinksData>(cacheFile);

            // Seed initial local cache
            await File.WriteAllTextAsync(cacheFile, TestAppHarness.SyntheticLinksJson);

            var result = await LoadBenchmarkRunner.MeasureAsync(
                scenarioName: "5. RemoteWithBackUpLoader Parse + Cache Stamp Comparison",
                asyncAction: async () =>
                {
                    var data = await RemoteWithBackUpLoader.LoadAsync(
                        http: http,
                        remoteUrl: "https://box.arizona.edu/links.json",
                        fileCache: cache,
                        stampSelector: d => d.Meta.LastUpdatedUtc,
                        normalize: d => d.Meta.Normalize());

                    Assert.NotNull(data);
                    Assert.Equal(50, data.CommonLinks.Count);
                },
                warmIterations: 60,
                output: output);

            Assert.True(result.WarmMedianMs >= 0);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void T06_WpfThemeAndViews_InstantiationAndLayout_LoadTime()
    {
        var result = LoadBenchmarkRunner.RunOnStaThread(() =>
        {
            TestAppHarness.EnsureWpfAppInitialized();
            var mainVm = App.Services.GetRequiredService<MainViewModel>();

            return LoadBenchmarkRunner.Measure(
                scenarioName: "6. WPF Views XAML Instantiation + Measure/Arrange Layout Pass",
                action: () =>
                {
                    var layoutSize = new Size(1280, 800);
                    var layoutRect = new Rect(layoutSize);

                    FrameworkElement[] views =
                    [
                        new HomeView { DataContext = mainVm.HomeVM },
                        new UserView { DataContext = mainVm.UserVM },
                        new ComputerView { DataContext = mainVm.ComputerVM },
                        new GroupView { DataContext = mainVm.GroupVM },
                        new LinksView { DataContext = mainVm.LinksVM },
                        new SettingsView { DataContext = mainVm.SettingsVM },
                        new AdminView { DataContext = mainVm.AdminVM },
                        new AboutView { DataContext = mainVm.AboutVM }
                    ];

                    foreach (var view in views)
                    {
                        view.Measure(layoutSize);
                        view.Arrange(layoutRect);
                        view.UpdateLayout();
                        view.DataContext = null;
                    }
                },
                warmIterations: 30,
                output: output);
        });

        Assert.True(result.WarmMedianMs >= 0);
    }
}
