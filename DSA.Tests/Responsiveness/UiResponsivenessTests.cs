using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DSA.Tests.Infrastructure;
using DSAMVVM;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces.AD;
using DSAMVVM.Core.Interfaces.Infrastructure;
using DSAMVVM.Core.Interfaces.Integrations;
using DSAMVVM.Core.Interfaces.Search;
using DSAMVVM.Core.Interfaces.UI;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Services.Integrations;
using DSAMVVM.MVVM.Model.AD;
using DSAMVVM.MVVM.View;
using DSAMVVM.MVVM.ViewModel;
using DSAMVVM.MVVM.ViewModel.Cards;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DSA.Tests.Responsiveness;

[TestCaseOrderer("DSA.Tests.Infrastructure.AlphabeticalOrderer", "DSA.Tests")]
[Collection("SequentialLoadBenchmarks")]
public class UiResponsivenessTests(ITestOutputHelper output)
{
    [Fact]
    public void R01_ShellNavigation_And_ContentHostRebind_Responsiveness()
    {
        var result = LoadBenchmarkRunner.RunOnStaThread(() =>
        {
            TestAppHarness.EnsureWpfAppInitialized();
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            mainVm.BootstrapInitialView();

            var host = new ContentControl { Width = 1280, Height = 800 };
            var layoutSize = new Size(1280, 800);
            var layoutRect = new Rect(layoutSize);

            // Pre-instantiate reusable view instances to measure warm shell tab switching + DataContext rebind + layout
            var homeView = new HomeView();
            var userView = new UserView();
            var computerView = new ComputerView();
            var groupView = new GroupView();
            var linksView = new LinksView();
            var settingsView = new SettingsView();

            (AppView ViewEnum, FrameworkElement ViewControl)[] sequence =
            [
                (AppView.User, userView),
                (AppView.Computer, computerView),
                (AppView.Group, groupView),
                (AppView.Links, linksView),
                (AppView.Settings, settingsView),
                (AppView.Home, homeView)
            ];

            return LoadBenchmarkRunner.Measure(
                scenarioName: "R1. Shell Tab Navigation Cycle (6 Views + Layout Rebind)",
                action: () =>
                {
                    foreach (var (viewEnum, viewControl) in sequence)
                    {
                        mainVm.SelectedView = viewEnum;
                        viewControl.DataContext = mainVm.CurrentView;
                        host.Content = viewControl;
                        host.Measure(layoutSize);
                        host.Arrange(layoutRect);
                        host.UpdateLayout();
                    }

                    Assert.Equal(AppView.Home, mainVm.SelectedView);
                },
                warmIterations: 60,
                output: output);
        });

        Assert.True(result.WarmMedianMs >= 0);
    }

    [Fact]
    public void R02_GroupCard_LiveKeystrokeFiltering_Responsiveness()
    {
        var result = LoadBenchmarkRunner.RunOnStaThread(() =>
        {
            TestAppHarness.EnsureWpfAppInitialized();

            var ad = App.Services.GetRequiredService<IADService>();
            var dept = App.Services.GetRequiredService<IDepartmentService>();
            var router = App.Services.GetRequiredService<IDeepLinkRoutingService>();

            var mimGroups = Enumerable.Range(1, 200)
                .Select(i => $"UA-MIM-Dept{(i % 25) + 1:D2}-Group-{i:D3}-WrkstUsers")
                .ToList();
            mimGroups.Add("UA-MIM-Wrkst-AllDivUsers");

            var members = Enumerable.Range(1, 500)
                .Select(i => $"wildcat_tech_{i:D4} (Technician {i:D4})")
                .ToList();

            var mimCard = GroupHistoryItemViewModel.CreateUserMim(
                "testuser",
                new MimLookupResult { Exists = true, Enabled = true, Groups = mimGroups },
                ad,
                dept,
                router);

            var memberCard = GroupHistoryItemViewModel.CreateGroupMembers(
                "UA-ITS-AllTechs",
                "UA-ITS-AllTechs",
                new ADGroupInfo { Exists = true, MemberCount = members.Count, GroupMembers = members },
                ad,
                dept,
                router);

            string[] mimKeystrokes = ["U", "UA-", "UA-MIM", "UA-MIM-Dept", "UA-MIM-Dept0", "UA-MIM-Dept05", "UA-MIM-Dept", ""];
            string[] memberKeystrokes = ["w", "wi", "wil", "wildcat_tech_", "wildcat_tech_0", "wildcat_tech_02", "wildcat_tech_025", ""];

            return LoadBenchmarkRunner.Measure(
                scenarioName: "R2. Group Card Live Keystroke Filtering (200 MIM + 500 Members)",
                action: () =>
                {
                    foreach (var stroke in mimKeystrokes)
                    {
                        mimCard.MimFilter = stroke;
                    }

                    foreach (var stroke in memberKeystrokes)
                    {
                        memberCard.MemberFilter = stroke;
                    }

                    Assert.Equal(201, mimCard.FilteredMimGroupCount);
                    Assert.Equal(500, memberCard.FilteredMemberCount);
                },
                warmIterations: 80,
                output: output);
        });

        Assert.True(result.WarmMedianMs >= 0);
    }

    [Fact]
    public async Task R03_LinksViewModel_TeamSwitching_Responsiveness()
    {
        LoadBenchmarkRunner.RunOnStaThread(TestAppHarness.EnsureWpfAppInitialized);

        string tempDir = Path.Combine(Path.GetTempPath(), $"DSA_LinksResp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string linksFile = Path.Combine(tempDir, "links.json");

        try
        {
            await File.WriteAllTextAsync(linksFile, TestAppHarness.SyntheticLinksJson);
            App.Settings.Paths.LinksData.UseCustomSource = true;
            App.Settings.Paths.LinksData.Source = "File";
            App.Settings.Paths.LinksData.Uri = linksFile;

            var http = new TestAppHarness.FakeHttpService();
            var settingsSvc = new TestAppHarness.IsolatedSettingsService();
            var linksService = new LinksService(http);
            await linksService.ReloadLinksDataAsync();

            var linksVm = new LinksViewModel(linksService, settingsSvc);
            await linksVm.EnsureLoadedAsync();

            var teams = linksVm.TeamNames.ToList();
            Assert.Equal(25, teams.Count);

            var result = LoadBenchmarkRunner.RunOnStaThread(() =>
                LoadBenchmarkRunner.Measure(
                    scenarioName: "R3. LinksViewModel Team Switching Sweep (25 Teams x 12 Links)",
                    action: () =>
                    {
                        foreach (var team in teams)
                        {
                            linksVm.SelectedTeam = team;
                            Assert.Equal(12, linksVm.SelectedTeamLinksCount);
                        }
                    },
                    warmIterations: 100,
                    output: output));

            Assert.True(result.WarmMedianMs >= 0);
        }
        finally
        {
            App.Settings.Paths.LinksData.UseCustomSource = false;
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task R04_SearchCardFeed_RapidLookupAndCapRotation_Responsiveness()
    {
        LoadBenchmarkRunner.RunOnStaThread(TestAppHarness.EnsureWpfAppInitialized);

        string tempDir = Path.Combine(Path.GetTempPath(), $"DSA_SearchResp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string deptFile = Path.Combine(tempDir, "departments.json");

        try
        {
            await File.WriteAllTextAsync(deptFile, TestAppHarness.SyntheticDepartmentsJson);
            App.Settings.Paths.DepartmentData.UseCustomSource = true;
            App.Settings.Paths.DepartmentData.Source = "File";
            App.Settings.Paths.DepartmentData.Uri = deptFile;

            var deptService = App.Services.GetRequiredService<IDepartmentService>();
            await deptService.ReloadDataAsync();

            var searchService = App.Services.GetRequiredService<ISearchService>();
            var userVm = App.Services.GetRequiredService<UserViewModel>();
            var computerVm = App.Services.GetRequiredService<ComputerViewModel>();
            var groupVm = App.Services.GetRequiredService<GroupViewModel>();

            var result = await LoadBenchmarkRunner.MeasureAsync(
                scenarioName: "R4. Search Card Feed Lookup + History Cap Rotation (15 Lookups)",
                asyncAction: async () =>
                {
                    await LoadBenchmarkRunner.StaDispatcher.InvokeAsync(async () =>
                    {
                        for (int i = 1; i <= 5; i++)
                        {
                            await userVm.OnSearchUpdated(
                                new SearchContextDTO($"tech{i:D3}"),
                                searchService,
                                SearchTarget.User);

                            await computerVm.OnSearchUpdated(
                                new SearchContextDTO($"UITS-PC-{i:D3}"),
                                searchService,
                                SearchTarget.Computer);

                            groupVm.SetSearchMode(GroupViewModel.GroupSearchMode.Department);
                            await groupVm.OnSearchUpdated(
                                new SearchContextDTO($"{i:D4}"),
                                searchService,
                                SearchTarget.Group);
                        }

                        userVm.CollapseAll();
                        userVm.ExpandAll();
                        Assert.NotEmpty(userVm.History);
                        Assert.NotEmpty(computerVm.History);
                        Assert.NotEmpty(groupVm.History);
                    }).Task.Unwrap();
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
    public void R05_Typography_FontZoomBroadcast_And_Relayout_Responsiveness()
    {
        var result = LoadBenchmarkRunner.RunOnStaThread(() =>
        {
            TestAppHarness.EnsureWpfAppInitialized();
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var notifier = App.Services.GetRequiredService<IOutputTextSettingsProvider>();

            var userView = new UserView { DataContext = mainVm.UserVM, Width = 1280, Height = 800 };
            var groupView = new GroupView { DataContext = mainVm.GroupVM, Width = 1280, Height = 800 };
            var layoutSize = new Size(1280, 800);
            var layoutRect = new Rect(layoutSize);

            userView.Measure(layoutSize);
            userView.Arrange(layoutRect);
            userView.UpdateLayout();

            groupView.Measure(layoutSize);
            groupView.Arrange(layoutRect);
            groupView.UpdateLayout();

            return LoadBenchmarkRunner.Measure(
                scenarioName: "R5. Font Zoom Broadcast + CardTypography Recalc + Relayout (10 Steps)",
                action: () =>
                {
                    for (int i = 0; i < 4; i++)
                    {
                        mainVm.UserVM.AdjustFont(+1);
                        userView.UpdateLayout();
                        groupView.UpdateLayout();
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        mainVm.UserVM.AdjustFont(-1);
                        userView.UpdateLayout();
                        groupView.UpdateLayout();
                    }

                    mainVm.UserVM.ResetFont();
                    notifier.NotifyChanged();
                    userView.UpdateLayout();
                    groupView.UpdateLayout();

                    Assert.True(mainVm.UserVM.EffectiveFontSize >= 8);
                },
                warmIterations: 80,
                output: output);
        });

        Assert.True(result.WarmMedianMs >= 0);
    }

    [Fact]
    public async Task R06_Dispatcher_InputAndRenderQueue_Latency_UnderContention()
    {
        LoadBenchmarkRunner.RunOnStaThread(TestAppHarness.EnsureWpfAppInitialized);

        var result = await LoadBenchmarkRunner.MeasureAsync(
            scenarioName: "R6. Dispatcher Input/Render Queue Turnaround (50 UI Frames Under Load)",
            asyncAction: async () =>
            {
                // Spawn background JSON parsing contention on the thread pool while pumping 50 UI priority frames
                var bgContention = Task.Run(() =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        _ = Newtonsoft.Json.JsonConvert.DeserializeObject<DSAMVVM.MVVM.Model.Data.DepartmentListWrapper>(
                            TestAppHarness.SyntheticDepartmentsJson);
                    }
                });

                int processed = 0;
                for (int i = 0; i < 25; i++)
                {
                    await LoadBenchmarkRunner.StaDispatcher.InvokeAsync(
                        () => { processed++; },
                        DispatcherPriority.Input).Task;

                    await LoadBenchmarkRunner.StaDispatcher.InvokeAsync(
                        () => { processed++; },
                        DispatcherPriority.Render).Task;
                }

                await bgContention;
                Assert.Equal(50, processed);
            },
            warmIterations: 60,
            output: output);

        Assert.True(result.WarmMedianMs >= 0);
    }
}
