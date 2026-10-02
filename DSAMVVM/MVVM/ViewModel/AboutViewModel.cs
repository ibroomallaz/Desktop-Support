using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Services.Updates;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public sealed record DeveloperItem(string Name, string NetId, string Role, string Initials, string Email);

    // Exposes application metadata, diagnostics, developer profiles, and version management commands to the About view
    public sealed class AboutViewModel : ObservableObject
    {
        private bool _showExtendedVersion;
        private bool _busy;
        private UpdateStatus _updateStatus = UpdateStatus.Idle;
        private string? _lastCheckedText;

        // Dynamically outputs either the base semantic version or the extended file version
        public string AppVersion => _showExtendedVersion
            ? $"{Globals.g_AppVersion} ({Globals.g_FileVersion})"
            : Globals.g_AppVersion;

        public bool IsBusy
        {
            get => _busy;
            private set
            {
                if (_busy == value) return;
                _busy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CheckButtonText));
            }
        }

        public string CheckButtonText => IsBusy ? "Checking..." : "Check for Updates";

        public UpdateStatus CurrentUpdateStatus
        {
            get => _updateStatus;
            private set
            {
                if (_updateStatus != value)
                {
                    _updateStatus = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsUpToDate));
                    OnPropertyChanged(nameof(IsChecking));
                    OnPropertyChanged(nameof(HasCheckFailed));
                }
            }
        }

        public bool IsUpToDate => CurrentUpdateStatus == UpdateStatus.UpToDate;
        public bool IsChecking => CurrentUpdateStatus == UpdateStatus.Checking;
        public bool HasCheckFailed => CurrentUpdateStatus == UpdateStatus.Failed;

        public string? LastCheckedText
        {
            get => _lastCheckedText;
            private set
            {
                if (_lastCheckedText != value)
                {
                    _lastCheckedText = value;
                    OnPropertyChanged();
                }
            }
        }

        // Developer Roster
        public IReadOnlyList<DeveloperItem> DeveloperList { get; } =
        [
            new DeveloperItem("Isaac Broomall", "ibroomall", "Lead Developer", "IB", "ibroomall@arizona.edu"),
            new DeveloperItem("JJ Velasquez", "jjvelasquez", "Birb Developer", "JV", "jjvelasquez@arizona.edu")
        ];

        // Backwards compatibility list
        public IReadOnlyList<string> Developers => DeveloperList.Select(d => $"{d.Name} ({d.NetId})").ToList();

        // System & Diagnostics Specs
        public string DeviceName => Environment.MachineName;
        public string DotNetVersion => RuntimeInformation.FrameworkDescription;
        public string OsVersion => GetFriendlyOsVersion();
        public string Architecture => RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "64-bit (x64)",
            System.Runtime.InteropServices.Architecture.Arm64 => "64-bit (ARM64)",
            System.Runtime.InteropServices.Architecture.X86 => "32-bit (x86)",
            System.Runtime.InteropServices.Architecture.Arm => "32-bit (ARM)",
            var arch => arch.ToString()
        };
        public string DirectoryStatus => GetDirectoryJoinStatus();
        public string Domain => DirectoryStatus;
        public string LogsPath => Globals.g_LogsDir;
        public string AppDataPath => Globals.g_AppDir;

        // Commands
        public ICommand OpenGitHubCommand { get; }
        public ICommand OpenSharePointCommand { get; }
        public ICommand OpenChangeLogCommand { get; }
        public ICommand CheckVersionCommand { get; }
        public ICommand ToggleVersionCommand { get; }
        public ICommand CopyVersionCommand { get; }
        public ICommand OpenLogsFolderCommand { get; }
        public ICommand OpenAppDataFolderCommand { get; }
        public ICommand EmailDeveloperCommand { get; }

        public event EventHandler<string>? OpenUrlRequested;

        private readonly IHttpService _http;
        private readonly IUpdaterService _updater;

        public AboutViewModel(IHttpService http, IUpdaterService updater)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _updater = updater ?? throw new ArgumentNullException(nameof(updater));

            OpenGitHubCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, Globals.g_GitHubUrl));
            OpenSharePointCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, Globals.g_SharepointHome));
            OpenChangeLogCommand = new RelayCommand(_ => OpenUrlRequested?.Invoke(this, Globals.g_ChangeLogURL));
            CheckVersionCommand = new RelayCommand(_ => ExecuteCheckVersion(), _ => !IsBusy);

            // Flips the boolean and notifies the UI to refresh the AppVersion string
            ToggleVersionCommand = new RelayCommand(_ =>
            {
                _showExtendedVersion = !_showExtendedVersion;
                OnPropertyChanged(nameof(AppVersion));
            });

            CopyVersionCommand = new RelayCommand(_ =>
            {
                try
                {
                    Clipboard.SetText(AppVersion);
                    UiNotify.Success($"Version {AppVersion} copied to clipboard.", showStatusBar: true);
                }
                catch (Exception ex)
                {
                    UiNotify.Warn($"Could not copy version to clipboard: {ex.Message}");
                }
            });

            OpenLogsFolderCommand = new RelayCommand(_ =>
            {
                try
                {
                    Directory.CreateDirectory(Globals.g_LogsDir);
                    Process.Start(new ProcessStartInfo { FileName = Globals.g_LogsDir, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    UiNotify.Error("Open Logs Folder", $"Could not open logs folder: {ex.Message}", ex);
                }
            });

            OpenAppDataFolderCommand = new RelayCommand(_ =>
            {
                try
                {
                    Directory.CreateDirectory(Globals.g_AppDir);
                    Process.Start(new ProcessStartInfo { FileName = Globals.g_AppDir, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    UiNotify.Error("Open App Data Folder", $"Could not open app data folder: {ex.Message}", ex);
                }
            });

            EmailDeveloperCommand = new RelayCommand(param =>
            {
                if (param is string email && !string.IsNullOrWhiteSpace(email))
                {
                    OpenUrlRequested?.Invoke(this, $"mailto:{email}");
                }
            });
        }

        private void ExecuteCheckVersion()
        {
            _ = CheckVersionAsync();
        }

        // Executes a manual version check pipeline and manages UI state tracking
        private async Task CheckVersionAsync()
        {
            if (IsBusy) return;
            const string key = "VersionCheck";
            IsBusy = true;
            CurrentUpdateStatus = UpdateStatus.Checking;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                UiNotify.Progress(UiNotify.ProgressOf(key), "Checking for updates…");

                var appSettings = App.Services.GetRequiredService<AppSettings>();
                var checker = new VersionCheckerUI(_http, _updater, appSettings);
                bool updateFound = await checker.CheckWithResultAsync(showUpToDatePopup: false);

                if (updateFound)
                {
                    CurrentUpdateStatus = UpdateStatus.UpdateAvailable;
                    LastCheckedText = $"Update available • {DateTime.Now:t}";
                }
                else
                {
                    CurrentUpdateStatus = UpdateStatus.UpToDate;
                    LastCheckedText = $"Checked at {DateTime.Now:t}";
                    UiNotify.Success($"Desktop Support App is up to date (v{AppVersion}).", showStatusBar: true);
                }
            }
            catch (Exception ex)
            {
                CurrentUpdateStatus = UpdateStatus.Failed;
                LastCheckedText = "Check failed";
                UiNotify.Error("Version Check", $"Check for updates failed: {ex.Message}", ex);
            }
            finally
            {
                UiNotify.RemoveKey(UiNotify.ProgressOf(key));
                IsBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Retrieves user-friendly OS edition and build information
        private static string GetFriendlyOsVersion()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key != null)
                {
                    var product = key.GetValue("ProductName") as string ?? "Windows";
                    var displayVer = key.GetValue("DisplayVersion") as string;
                    var buildStr = key.GetValue("CurrentBuild") as string;

                    if (int.TryParse(buildStr, out var build) && build >= 22000)
                    {
                        product = product.Replace("Windows 10", "Windows 11");
                    }

                    var details = new List<string>();
                    if (!string.IsNullOrWhiteSpace(displayVer)) details.Add(displayVer);
                    if (!string.IsNullOrWhiteSpace(buildStr)) details.Add($"Build {buildStr}");

                    return details.Count > 0
                        ? $"{product} ({string.Join(", ", details)})"
                        : product;
                }
            }
            catch
            {
                // ignored
            }

            var b = Environment.OSVersion.Version.Build;
            var w = b >= 22000 ? "Windows 11" : "Windows 10";
            return $"{w} (Build {b})";
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int NetGetJoinInformation(string? server, out IntPtr nameBuffer, out int status);

        [DllImport("netapi32.dll")]
        private static extern int NetApiBufferFree(IntPtr buffer);

        // Dynamically resolves whether machine is AD domain joined, Entra joined, hybrid, or workgroup
        private static string GetDirectoryJoinStatus()
        {
            try
            {
                bool isDomainJoined = false;
                string joinName = "";

                if (NetGetJoinInformation(null, out var pBuffer, out var status) == 0)
                {
                    try
                    {
                        joinName = Marshal.PtrToStringUni(pBuffer) ?? "";
                        if (status == 3) // NetSetupDomainName
                        {
                            isDomainJoined = true;
                        }
                    }
                    finally
                    {
                        NetApiBufferFree(pBuffer);
                    }
                }

                bool isEntraJoined = false;
                try
                {
                    using var cloudKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo");
                    if (cloudKey != null && cloudKey.GetSubKeyNames().Length > 0)
                    {
                        isEntraJoined = true;
                    }
                }
                catch
                {
                    // ignored
                }

                if (isDomainJoined && isEntraJoined)
                    return $"{joinName} (Hybrid Entra)";
                if (isDomainJoined)
                    return $"{joinName} (Domain)";
                if (isEntraJoined)
                    return "Entra ID (Cloud)";

                if (!string.IsNullOrWhiteSpace(joinName))
                    return $"Workgroup: {joinName}";

                return "Workgroup (Standalone)";
            }
            catch
            {
                return "Standalone (Unbound)";
            }
        }
    }
}
