using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model.Schemas;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace DSAMVVM.Core.Services.Updates
{
    public class UpdaterService : IUpdaterService
    {
        public async Task DownloadAndInstallAsync(CurrentVersion updateInfo, IProgress<string>? progressReporter = null)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"DSA_Update_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            var msiPath = Path.Combine(tempDir, "DSA_Installer.msi");
            var setupPath = Path.Combine(tempDir, "setup.exe");

            var needsFramework = !string.IsNullOrWhiteSpace(updateInfo.RequiredDotNetVersion) &&
                                 !IsDotNetRuntimeInstalled(updateInfo.RequiredDotNetVersion);

            try
            {
                Log.Info("UpdaterSvc", $"Starting background download to: {tempDir}");

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
                {
                    if (needsFramework && !string.IsNullOrWhiteSpace(updateInfo.SetupUrl) && !string.IsNullOrWhiteSpace(updateInfo.MsiUrl))
                    {
                        progressReporter?.Report("Downloading .NET bootstrapper...");
                        await DownloadFileAsync(client, updateInfo.SetupUrl, setupPath);

                        progressReporter?.Report("Downloading update...");
                        await DownloadFileAsync(client, updateInfo.MsiUrl, msiPath);

                        progressReporter?.Report("Update ready. App will restart shortly...");
                        await Task.Delay(1500); // Give tech a moment to read

                        ExecuteInstaller(setupPath, "/quiet /norestart");
                    }
                    else if (!string.IsNullOrWhiteSpace(updateInfo.MsiUrl))
                    {
                        progressReporter?.Report("Downloading update...");
                        await DownloadFileAsync(client, updateInfo.MsiUrl, msiPath);

                        progressReporter?.Report("Update ready. App will restart shortly...");
                        await Task.Delay(1500);

                        ExecuteInstaller("msiexec.exe", $"/i \"{msiPath}\" /qn /norestart");
                    }
                    else
                    {
                        throw new InvalidOperationException("No valid download URLs provided.");
                    }
                }

                // Shutdown current instance so the MSI can overwrite files
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                Log.Error("UpdaterSvc", $"Update failed: {ex.Message}");
                HandleFailure(ex.Message, updateInfo.Location, tempDir);
            }
            finally
            {
                progressReporter?.Report("Ready");
            }
        }

        // --- ATOMIC DOWNLOAD PATTERN ---
        // Downloads as .download and renames only after successful completion
        private static async Task DownloadFileAsync(HttpClient client, string url, string destinationPath)
        {
            var tempPath = destinationPath + ".download";
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }
            File.Move(tempPath, destinationPath, true);
        }

        // --- POWERSHELL WATCHDOG ---
        // Launches the installer and waits for it to exit before restarting this app
        private static void ExecuteInstaller(string fileName, string arguments)
        {
            string? appPath = Process.GetCurrentProcess().MainModule?.FileName;

            if (!string.IsNullOrEmpty(appPath))
            {
                // PowerShell waits for the installer (-Wait), then restarts the original appPath
                string psCommand = $"-Command \"Start-Process '{fileName}' -ArgumentList '{arguments}' -Wait; Start-Process '{appPath}' -ArgumentList '-updated'\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = psCommand,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                });
            }
            else
            {
                // Fallback if we can't determine the current EXE path
                Process.Start(new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = true });
            }
        }

        private static bool IsDotNetRuntimeInstalled(string requiredVersionString)
        {
            if (!Version.TryParse(requiredVersionString, out var requiredVersion)) return false;
            var runtimePath = @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App";
            if (!Directory.Exists(runtimePath)) return false;

            return Directory.GetDirectories(runtimePath)
                .Select(Path.GetFileName)
                .Any(v => Version.TryParse(v, out var installed) && installed >= requiredVersion);
        }

        private static void HandleFailure(string userMessage, string? fallbackUrl, string tempDir)
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }

            var result = MessageBox.Show($"{userMessage}\n\nDownload manually?", "Update Failed", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(fallbackUrl))
            {
                Process.Start(new ProcessStartInfo { FileName = fallbackUrl, UseShellExecute = true });
            }
        }

        public async Task CleanupOldUpdatesAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(60));
            await Task.Run(() =>
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), "DSA_Update_*"))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
                catch { }
            });
        }
    }
}