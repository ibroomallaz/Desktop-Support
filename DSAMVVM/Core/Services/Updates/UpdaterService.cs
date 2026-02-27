using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;

namespace DSAMVVM.MVVM.Services.Updates
{
    public class UpdaterService : IUpdaterService
    {
        public async Task DownloadAndInstallAsync(string downloadUrl, IProgress<string>? progressReporter = null)
        {
            // Generates a temporary file path for the installer payload
            var fileName = $"DSA_Update_{Guid.NewGuid().ToString("N")[..8]}.msi";
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            try
            {
                Log.Info("UpdaterSvc", $"Starting background download to: {tempPath}");
                progressReporter?.Report("Connecting to update server...");

                // Initializes HTTP client with a 60-second timeout to prevent indefinite hangs
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    progressReporter?.Report("Downloading update...");

                    // Streams the HTTP response directly to the local file system
                    using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream);
                }

                progressReporter?.Report("Preparing to install...");
                Log.Info("UpdaterSvc", "Download successful. Launching installer.");

                // Configures the process to launch the downloaded installer via the OS shell
                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                };
                Process.Start(psi);

                // Terminates the current application process to release file locks for the installer execution
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (HttpRequestException netEx)
            {
                Log.Error("UpdaterSvc", $"Network error: {netEx.Message}");
                HandleFailure("A network connection issue interrupted the download.", downloadUrl, tempPath);
            }
            catch (IOException ioEx)
            {
                Log.Error("UpdaterSvc", $"File system error: {ioEx.Message}");
                HandleFailure("Unable to save the update file. Your disk may be full, or an antivirus blocked it.", downloadUrl, tempPath);
            }
            catch (System.ComponentModel.Win32Exception winEx)
            {
                Log.Error("UpdaterSvc", $"Execution blocked: {winEx.Message}");
                HandleFailure("Windows prevented the installer from launching. This is usually caused by strict security policies.", downloadUrl, tempPath);
            }
            catch (Exception ex)
            {
                Log.Error("UpdaterSvc", $"Unexpected error: {ex.Message}");
                HandleFailure($"An unexpected error occurred: {ex.Message}", downloadUrl, tempPath);
            }
            finally
            {
                progressReporter?.Report("Ready");
            }
        }

        // Manages the cleanup of temporary files and provides a manual download fallback to the user
        private void HandleFailure(string userMessage, string fallbackUrl, string tempFileToDelete)
        {
            // Removes the incomplete or corrupted temporary file
            try
            {
                if (File.Exists(tempFileToDelete))
                {
                    File.Delete(tempFileToDelete);
                }
            }
            catch { }

            var result = MessageBox.Show(
                $"{userMessage}\n\nWould you like to open your browser to download the update manually?",
                "Automated Update Failed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
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
    }
}