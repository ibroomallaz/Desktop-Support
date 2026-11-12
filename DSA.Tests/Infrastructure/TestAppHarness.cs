using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using DSAMVVM;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces.AD;
using DSAMVVM.Core.Interfaces.Graph;
using DSAMVVM.Core.Interfaces.Infrastructure;
using DSAMVVM.Core.Interfaces.Search;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Services.Infrastructure;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Model.Config.UI;
using DSAMVVM.MVVM.Model.Data;
using DSAMVVM.MVVM.Model.Schemas;
using DSAMVVM.MVVM.Services.Status;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;

namespace DSA.Tests.Infrastructure;

/// <summary>
/// Provides a deterministic, offline-isolated test harness for measuring DSAMVVM startup and load times
/// without external network jitter, Active Directory LDAP timeouts, or WAM broker prompts.
/// </summary>
public static class TestAppHarness
{
    private static readonly Lock InitLock = new();
    private static bool _wpfInitialized;

    public static string SyntheticDepartmentsJson { get; } = BuildSyntheticDepartmentsJson(deptCount: 500, teamCount: 35);
    public static string SyntheticLinksJson { get; } = BuildSyntheticLinksJson(commonCount: 50, teamCount: 25, linksPerTeam: 12);
    public static string SyntheticServiceMeowJson { get; } = BuildSyntheticServiceMeowJson(ownerCount: 40, petsPerOwner: 2);
    public static string SyntheticSettingsJson { get; } = BuildSyntheticSettingsJson();

    /// <summary>
    /// Ensures the singleton WPF <see cref="App"/> instance and its <see cref="Application.Resources"/>
    /// are initialized on an STA thread, and binds <see cref="App.Services"/> and <see cref="App.Settings"/>.
    /// </summary>
    public static void EnsureWpfAppInitialized()
    {
        lock (InitLock)
        {
            if (!_wpfInitialized)
            {
                if (Application.Current == null)
                {
                    var appAssembly = typeof(App).Assembly;
                    typeof(Application)
                        .GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)
                        ?.SetValue(null, appAssembly);
                    typeof(System.Windows.Navigation.BaseUriHelper)
                        .GetProperty("ResourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)
                        ?.SetValue(null, appAssembly);

                    var app = new App();
                    app.InitializeComponent();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                }

                _wpfInitialized = true;
            }

            var provider = BuildTestServiceProvider();
            SetAppServices(provider);
            App.Settings = CreateDefaultTestSettings();

            var bus = provider.GetRequiredService<StatusBus>();
            UiNotify.Initialize(bus.Report, bus.RemoveByKey, bus.Clear);
            Log.Initialize(provider.GetRequiredService<IAppLogger>(), min: AppLogLevel.Off);
        }
    }

    public static void SetAppServices(IServiceProvider provider)
    {
        var prop = typeof(App).GetProperty(nameof(App.Services), BindingFlags.Public | BindingFlags.Static);
        prop?.SetValue(null, provider);

        if (Application.Current is App app)
        {
            typeof(App).GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(app, provider);
        }
    }

    /// <summary>
    /// Builds the full DSAMVVM DI container using production registrations, overriding only external I/O
    /// services (HTTP, AD, Network, MSAL Auth, FileLogger) with deterministic in-memory fakes.
    /// </summary>
    public static IServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddCoreServices();
        services.AddViewModels();

        // Replace external network/OS-dependent singletons with deterministic, zero-latency fakes
        services.Replace(ServiceDescriptor.Singleton<IAppLogger, NullAppLogger>());
        services.Replace(ServiceDescriptor.Singleton<ISettingsService, IsolatedSettingsService>());
        services.Replace(ServiceDescriptor.Singleton<IHttpService>(_ => new FakeHttpService()));
        services.Replace(ServiceDescriptor.Singleton<INetworkDetectionService, FakeNetworkDetectionService>());
        services.Replace(ServiceDescriptor.Singleton<IADDetectionService, FakeADDetectionService>());
        services.Replace(ServiceDescriptor.Singleton<IAuthenticationService, FakeAuthenticationService>());
        services.Replace(ServiceDescriptor.Singleton<IADService, FakeADService>());
        services.Replace(ServiceDescriptor.Singleton<IQuickSearchService, FakeQuickSearchService>());

        return services.BuildServiceProvider();
    }

    public static AppSettings CreateDefaultTestSettings()
    {
        var s = JsonConvert.DeserializeObject<AppSettings>(SyntheticSettingsJson) ?? new AppSettings();
        s.ApplyDefaultsAndClamp();
        return s;
    }

    private static string BuildSyntheticDepartmentsJson(int deptCount, int teamCount)
    {
        var teams = Enumerable.Range(1, teamCount).Select(i => new SupportTeam
        {
            SupportTeamName = $"Support Team {i:D2}",
            ManagerName = $"Manager {i}",
            ManagerNetID = $"mgr{i:D2}",
            PhoneNumber = $"520-621-{1000 + i:D4}",
            SupportedDivisions =
            [
                new SupportedDivs { DivAbbrev = $"DV{i:D2}", DivFullName = $"Division {i}" },
                new SupportedDivs { DivAbbrev = $"SB{i:D2}", DivFullName = $"SubDivision {i}" }
            ]
        }).ToList();

        var depts = Enumerable.Range(1, deptCount).Select(i => new Department
        {
            Number = $"{i:D4}",
            SupportKnown = i % 10 != 0,
            Team = teams[i % teams.Count].SupportTeamName,
            Notes = $"Standard support notes for department {i:D4}. Assigned to {teams[i % teams.Count].SupportTeamName}.",
            FileRepoPath = $@"\\files.arizona.edu\dept\{i:D4}\installers"
        }).ToList();

        var wrapper = new DepartmentListWrapper
        {
            Meta = new DepartmentMeta { SchemaVersion = 1, LastUpdatedUtc = DateTime.UtcNow },
            SupportTeams = teams,
            DepartmentList = depts
        };

        return JsonConvert.SerializeObject(wrapper, Formatting.Indented);
    }

    private static string BuildSyntheticLinksJson(int commonCount, int teamCount, int linksPerTeam)
    {
        var common = Enumerable.Range(1, commonCount).Select(i => new Link
        {
            Name = $"Common Campus Portal {i:D2}",
            Description = $"Description for campus-wide technician resource {i:D2}",
            URL = $"https://support.arizona.edu/tools/{i:D2}"
        }).ToList();

        var teamGroups = Enumerable.Range(1, teamCount).Select(t => new TeamLinkGroup
        {
            Team = $"Support Team {t:D2}",
            Links = Enumerable.Range(1, linksPerTeam).Select(l => new Link
            {
                Name = $"Team {t:D2} Tool {l:D2}",
                Description = $"Internal runbook and dashboard {l:D2} for Support Team {t:D2}",
                URL = $"https://teams.arizona.edu/team-{t:D2}/tool-{l:D2}"
            }).ToList()
        }).ToList();

        var data = new LinksData
        {
            Meta = new LinksMeta { SchemaVersion = 1, LastUpdatedUtc = DateTime.UtcNow },
            CommonLinks = common,
            TeamLinks = teamGroups
        };

        return JsonConvert.SerializeObject(data, Formatting.Indented);
    }

    private static string BuildSyntheticServiceMeowJson(int ownerCount, int petsPerOwner)
    {
        var owners = Enumerable.Range(1, ownerCount).Select(o => new ServiceMeowOwner
        {
            NetId = $"tech{o:D3}",
            Name = $"Technician {o:D3}",
            Team = $"Support Team {(o % 15) + 1:D2}",
            Pets = Enumerable.Range(1, petsPerOwner).Select(p => new ServiceMeowPet
            {
                Name = $"Mascot {o:D2}-{p}",
                Title = "Senior Purr Engineer",
                Breed = p % 2 == 0 ? "Domestic Shorthair" : "Golden Retriever",
                Species = p % 2 == 0 ? "Cat" : "Dog",
                Blurb = $"Helps debug Active Directory tickets for Technician {o:D3}.",
                Images = [$"https://cdn.arizona.edu/pets/pet_{o}_{p}.jpg"]
            }).ToList()
        }).ToList();

        var data = new ServiceMeowData
        {
            Meta = new ServiceMeowMeta { SchemaVersion = 1, LastUpdatedUtc = DateTime.UtcNow },
            Owners = owners
        };

        return JsonConvert.SerializeObject(data, Formatting.Indented);
    }

    private static string BuildSyntheticSettingsJson()
    {
        var settings = new AppSettings();
        settings.Ui.Font.DefaultSize = 13;
        settings.Ui.Font.ViewFontSizeOverride = true;
        settings.Ui.ViewFontSizes["UserView"] = new ViewFontSetting { FontSize = 14 };
        settings.Ui.ViewFontSizes["ComputerView"] = new ViewFontSetting { FontSize = 12 };
        settings.Ui.ViewFontSizes["GroupView"] = new ViewFontSetting { FontSize = 13 };
        settings.Ui.Shortcuts.Items = Enumerable.Range(1, 6).Select(i => new HomeShortcutItem
        {
            Id = $"sc-{i}",
            Title = $"Shortcut {i}",
            Description = $"Quick action {i}",
            Target = $"https://arizona.edu/sc/{i}",
            Icon = "\uE71B",
            ColorPreset = "Blue"
        }).ToList();
        settings.ApplyDefaultsAndClamp();
        return JsonConvert.SerializeObject(settings, Formatting.Indented);
    }

    // --- Deterministic Fakes ---

    private sealed class NullAppLogger : IAppLogger
    {
        public void Write(AppLogLevel level, string tag, string message, Exception? ex = null) { }
    }

    public sealed class IsolatedSettingsService : ISettingsService
    {
        private readonly SettingsService _inner = new();
        public event EventHandler<AppSettings>? SettingsChanged;

        public Task<AppSettings> LoadAsync(string settingsPath, CancellationToken ct = default)
            => _inner.LoadAsync(settingsPath, ct);

        public Task SaveAsync(AppSettings settings, string settingsPath, CancellationToken ct = default)
            => Task.CompletedTask;

        public string ResolveDeptPath(AppSettings s) => _inner.ResolveDeptPath(s);
        public string ResolveLogDir(AppSettings s) => _inner.ResolveLogDir(s);
        public double GetFontSizeFor(string viewName, AppSettings s, double min = 8, double max = 24)
            => _inner.GetFontSizeFor(viewName, s, min, max);
        public double AdjustOutputFontSize(AppSettings s, string? viewName, int delta, bool preferPerView)
            => _inner.AdjustOutputFontSize(s, viewName, delta, preferPerView);
        public void ResetOutputFontSize(AppSettings s, string? viewName, bool preferPerView, int defaultSize = 12)
            => _inner.ResetOutputFontSize(s, viewName, preferPerView, defaultSize);

        public void RequestSave(AppSettings? s, string settingsPath)
        {
            if (s != null) SettingsChanged?.Invoke(this, s);
        }

        public void FlushPendingSaves() { }
    }

    public sealed class FakeHttpService : IHttpService
    {
        public Task<string> GetStringAsync(string url, CancellationToken ct = default)
            => ResolvePayloadAsync(url);

        public Task<string> GetStringAsync(string url, TimeSpan timeout, CancellationToken ct = default)
            => ResolvePayloadAsync(url);

        public Task<Stream> GetStreamAsync(string url, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("{}")));

        public Task DownloadFileAsync(string url, string filePath, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DownloadFileAsync(string url, string filePath, TimeSpan timeout, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        public bool TryOpenUrl(string target, out Exception? error)
        {
            error = null;
            return true;
        }

        public void Dispose() { }

        private static Task<string> ResolvePayloadAsync(string url)
        {
            if (url.Contains("dept", StringComparison.OrdinalIgnoreCase) || url.Contains("46p3", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(SyntheticDepartmentsJson);
            if (url.Contains("link", StringComparison.OrdinalIgnoreCase) || url.Contains("v03k", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(SyntheticLinksJson);
            if (url.Contains("meow", StringComparison.OrdinalIgnoreCase) || url.Contains("2b7z", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(SyntheticServiceMeowJson);

            return Task.FromResult(SyntheticDepartmentsJson);
        }
    }

    private sealed class FakeNetworkDetectionService : INetworkDetectionService
    {
        public NetworkStateInfo CurrentState { get; } = new()
        {
            ConnectionType = NetworkConnectionType.CampusWired,
            AdapterName = "Ethernet0",
            PrimaryIp = "128.196.10.25",
            DnsSuffix = "arizona.edu",
            Label = "Campus Wired"
        };

        public event EventHandler<NetworkStateInfo>? NetworkStateChanged
        {
            add { }
            remove { }
        }

        public NetworkStateInfo Refresh() => CurrentState;
        public Task<NetworkStateInfo> RefreshAsync() => Task.FromResult(CurrentState);
        public void Dispose() { }
    }

    private sealed class FakeADDetectionService : IADDetectionService
    {
        public ADStateInfo CurrentState { get; } = ADStateInfo.Reachable("bluecat.arizona.edu", "128.196.1.1", 4);
        public event Action<ADStateInfo>? StateChanged
        {
            add { }
            remove { }
        }

        public Task<ADStateInfo> ProbeDomainControllerAsync(CancellationToken ct = default)
            => Task.FromResult(CurrentState);
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public bool IsAuthenticated => true;
        public string? CurrentAccountUpn => "testtech@arizona.edu";
        public event Action<bool>? AuthenticationStateChanged
        {
            add { }
            remove { }
        }

        public Task<bool> ValidateAuthenticationAsync(string[] scopes) => Task.FromResult(true);
        public Task<bool> CheckCachedSignInAsync() => Task.FromResult(true);
        public Task<string> GetGraphAccessTokenAsync(string[] scopes) => Task.FromResult("fake-jwt-token");
        public Task<string> AcquireTokenInteractiveAsync(string[] scopes, IntPtr? parentWindowHandle = null) => Task.FromResult("fake-jwt-token");
        public void ProcessAuthRedirect(string url) { }
        public Task SignOutAsync() => Task.CompletedTask;
    }

    private sealed class FakeADService : IADService
    {
        public Task<ADUserInfo> GetUserAsync(string netid) =>
            Task.FromResult(new ADUserInfo
            {
                Exists = true,
                Name = netid,
                DisplayName = $"User {netid}",
                DepartmentNumber = "0001",
                DepartmentName = "0001-IT Services",
                Division = "DV01"
            });

        public Task<string?> LookupNameByEmployeeID(string userNumber) =>
            Task.FromResult<string?>($"User {userNumber}");

        public Task<ADComputerInfo> GetComputerAsync(string hostname) =>
            Task.FromResult(new ADComputerInfo
            {
                Exists = true,
                Name = hostname,
                OperatingSystem = "Windows 11 Enterprise"
            });

        public Task<ADGroupInfo> GetGroupAsync(string groupName) =>
            Task.FromResult(new ADGroupInfo
            {
                Exists = true,
                MemberCount = 5,
                GroupMembers = ["user1", "user2", "user3", "user4", "user5"]
            });

        public Task<MimLookupResult> GetUserMimGroupsAsync(string netid) =>
            Task.FromResult(new MimLookupResult());

        public Task<AdobeLicenseStatus> CheckAdobeLicensesAsync(string netid) =>
            Task.FromResult(new AdobeLicenseStatus(true, true));
    }

    private sealed class FakeQuickSearchService : IQuickSearchService
    {
        public event EventHandler<string>? QuickSearchTriggered
        {
            add { }
            remove { }
        }

        public void Start() { }
        public void Stop() { }
        public void Configure(QuickSearchSettings settings) { }
    }
}
