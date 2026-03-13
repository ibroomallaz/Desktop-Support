using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model.Schemas;

namespace DSAMVVM.Core.Services.Updates
{
    public class UpdaterService : IUpdaterService
    {
        public async Task DownloadAndInstallAsync(CurrentVersion updateInfo, IProgress<string>? progressReporter = null)
        {
            // Creates an isolated temporary directory to house multiple installation files if necessary
            var tempDir = Path.Combine(Path.GetTempPath(), $"DSA_Update_{Guid.NewGuid().ToString("N")[..8]}");
            Directory.CreateDirectory(tempDir);

            var msiPath = Path.Combine(tempDir, "DSA_Installer.msi");
            var setupPath = Path.Combine(tempDir, "setup.exe");

            var needsFramework = !string.IsNullOrWhiteSpace(updateInfo.RequiredDotNetVersion) &&
                                 !IsDotNetRuntimeInstalled(updateInfo.RequiredDotNetVersion);

            try
            {
                Log.Info("UpdaterSvc", $"Starting background download to: {tempDir}");
                progressReporter?.Report("Connecting to update server...");

                // Initializes HTTP client with a 60-second timeout to prevent indefinite hangs
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
                {
                    if (needsFramework && !string.IsNullOrWhiteSpace(updateInfo.SetupUrl) && !string.IsNullOrWhiteSpace(updateInfo.MsiUrl))
                    {
                        progressReporter?.Report("Downloading .NET bootstrapper...");
                        await DownloadFileAsync(client, updateInfo.SetupUrl, setupPath);

                        progressReporter?.Report("Downloading update...");
                        await DownloadFileAsync(client, updateInfo.MsiUrl, msiPath);

                        progressReporter?.Report("Preparing to install...");
                        Log.Info("UpdaterSvc", "Downloads successful. Launching setup bootstrapper.");
                        ExecuteInstaller(setupPath, "/quiet /norestart");
                    }
                    else if (!string.IsNullOrWhiteSpace(updateInfo.MsiUrl))
                    {
                        progressReporter?.Report("Downloading update...");
                        await DownloadFileAsync(client, updateInfo.MsiUrl, msiPath);

                        progressReporter?.Report("Preparing to install...");
                        Log.Info("UpdaterSvc", "Download successful. Launching MSI directly.");
                        ExecuteInstaller("msiexec.exe", $"/i \"{msiPath}\" /qn /norestart");
                    }
                    else
                    {
                        throw new InvalidOperationException("No valid download URLs provided in the manifest.");
                    }
                }

                // Terminates the current application process to release file locks for the installer execution
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (HttpRequestException netEx)
            {
                Log.Error("UpdaterSvc", $"Network error: {netEx.Message}");
                HandleFailure("A network connection issue interrupted the download.", updateInfo.Location, tempDir);
            }
            catch (IOException ioEx)
            {
                Log.Error("UpdaterSvc", $"File system error: {ioEx.Message}");
                HandleFailure("Unable to save the update file. Your disk may be full, or an antivirus blocked it.", updateInfo.Location, tempDir);
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                Log.Error("UpdaterSvc", $"Execution blocked: {winEx.Message}");
                HandleFailure("Windows prevented the installer from launching. This is usually caused by strict security policies.", updateInfo.Location, tempDir);
            }
            catch (Exception ex)
            {
                Log.Error("UpdaterSvc", $"Unexpected error: {ex.Message}");
                HandleFailure($"An unexpected error occurred: {ex.Message}", updateInfo.Location, tempDir);
            }
            finally
            {
                progressReporter?.Report("Ready");
            }
        }

        // Streams the HTTP response directly to the local file system
        private static async Task DownloadFileAsync(HttpClient client, string url, string destinationPath)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
        }

        // Parses local dotnet directories to verify if a matching or higher runtime exists
        private static bool IsDotNetRuntimeInstalled(string requiredVersionString)
        {
            if (!Version.TryParse(requiredVersionString, out var requiredVersion)) return false;

            var runtimePath = @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App";
            if (!Directory.Exists(runtimePath)) return false;

            foreach (var dir in Directory.GetDirectories(runtimePath))
            {
                var folderName = Path.GetFileName(dir);
                if (Version.TryParse(folderName, out var installedVersion) && installedVersion >= requiredVersion)
                {
                    return true;
                }
            }
            return false;
        }

        // Configures the process to launch the designated executable via the OS shell
        private static void ExecuteInstaller(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        // Manages the cleanup of the temporary directory and provides a manual download fallback to the user
        private static void HandleFailure(string userMessage, string? fallbackUrl, string tempDirectoryToDelete)
        {
            // Recursively removes the temporary folder and any partial files inside it
            try
            {
                if (Directory.Exists(tempDirectoryToDelete))
                {
                    Directory.Delete(tempDirectoryToDelete, true);
                }
            }
            catch { }

            var result = MessageBox.Show(
                $"{userMessage}\n\nWould you like to open your browser to download the update manually?",
                "Automated Update Failed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(fallbackUrl))
            {
                // Launches the system default web browser to the provided download URL
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = fallbackUrl, UseShellExecute = true });
                }
                catch
                {
                    MessageBox.Show($"Please navigate to: {fallbackUrl}", "Browser Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        public async Task CleanupOldUpdatesAsync()
        {
            // Delays execution for 60 seconds to keep the app startup lightning fast
            await Task.Delay(TimeSpan.FromSeconds(60));

            await Task.Run(() =>
            {
                try
                {
                    var tempPath = Path.GetTempPath();
                    var updateDirs = Directory.GetDirectories(tempPath, "DSA_Update_*");

                    foreach (var dir in updateDirs)
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            Log.Info("UpdaterSvc", $"Cleaned up old update directory: {dir}");
                        }
                        catch (Exception ex)
                        {
                            // It is perfectly normal for files here to be locked by antivirus or the OS.
                            // We catch and ignore per-directory exceptions so it can just try again next time.
                            Log.Debug("UpdaterSvc", $"Could not delete orphaned update folder {dir}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("UpdaterSvc", "Failed to enumerate Temp directory for cleanup.", ex);
                }
            });
        }
    }
}