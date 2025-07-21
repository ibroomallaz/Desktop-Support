using DSAMVVM.Core;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace DSAMVVM.MVVM.Model.utils
{
    public class VersionChecker
    {
        private readonly HttpClient _client;
        private readonly IStatusReporter _status;

        public VersionChecker(IStatusReporter status, HttpClient? client = null)
        {
            _status = status ?? throw new ArgumentNullException(nameof(status));
            _client = client ?? new HttpClient();
            _ = CheckAsync();
        }

        private static bool BetaCheck()
        {
            string version = Globals.g_AppVersion;
            return version.Contains("beta", StringComparison.OrdinalIgnoreCase) || version.Contains("alpha", StringComparison.OrdinalIgnoreCase);
        }

        public async Task CheckAsync()
        {
            try
            {
                using HttpResponseMessage response = await _client.GetAsync(Globals.g_versionJSON);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var versionData = JsonConvert.DeserializeObject<Root>(json);

                if (versionData?.Version != null)
                    NotifyUser(versionData.Version);
            }
            catch (HttpRequestException e)
            {
                _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                    $"Version check HTTP Error: {e.Message}. {{0}}",
                    new Inline[]
                    {
                        StatusMessageFactory.ActionLink("Retry", () => _ = CheckAsync())
                    },
                    priority: 3,
                    sticky: true
                ));
            }
            catch (JsonException e)
            {
                _status.Report(StatusMessageFactory.CreateRichInternalMessage(
                    $"Version Check JSON Parse Error: {e.Message}. {{0}}",
                    new Inline[]
                    {
                        StatusMessageFactory.ActionLink("Retry", () => _ = CheckAsync())
                    },
                    priority: 3,
                    sticky: true
                ));
            }
        }

        private void NotifyUser(VersionInfo versionInfo)
        {
            string installedVersion = Globals.g_AppVersion;
            bool isBetaUser = BetaCheck();

            bool isStableUpdateAvailable = versionInfo.Current?.Version != null && IsNewerVersion(installedVersion, versionInfo.Current.Version);

            bool isBetaUpdateAvailable = versionInfo.PreRelease?.Exists == true && !string.IsNullOrEmpty(versionInfo.PreRelease.Version) && IsNewerVersion(installedVersion, versionInfo.PreRelease.Version);

            bool isBetaHigherThanStable = versionInfo.Current?.Version != null && versionInfo.PreRelease?.Version != null && IsNewerVersion(versionInfo.Current.Version, versionInfo.PreRelease.Version);

            if (isStableUpdateAvailable && !isBetaUser && versionInfo.Current != null)
            {
                var current = versionInfo.Current;
                PromptUpdate("Update Available", current.Version!, installedVersion, current.Location, current.Changelog, false);
            }


            if (isBetaUser)
            {
                if (isBetaUpdateAvailable)
                {
                    PromptUpdate("PreRelease Update Available", versionInfo.PreRelease!.Version!, installedVersion, versionInfo.PreRelease.Location, versionInfo.PreRelease.Changelog, true);
                }
                else if (!isBetaHigherThanStable && isStableUpdateAvailable)
                {
                    PromptUpdate("Stable Update Recommended", versionInfo.Current!.Version!, installedVersion, versionInfo.Current.Location, versionInfo.Current.Changelog, false);
                }
            }
        }

        private static void PromptUpdate(string title, string newVersion, string installedVersion, string? location, string? changelog, bool isBeta)
        {
            location ??= Globals.g_sharepointHome;
            changelog ??= "No details provided.";

            Application.Current.Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"{title}\n\nA new version ({newVersion}) is available.\n\nCurrent version: {installedVersion}\n\nChanges:\n{changelog}\n\nWould you like to update?",
                    title,
                    MessageBoxButton.OKCancel,
                    isBeta ? MessageBoxImage.Information : MessageBoxImage.Warning);

                if (result == MessageBoxResult.OK)
                    HTTP.OpenURL(location);
            });
        }

        private static bool IsNewerVersion(string? currentVersion, string? newVersion)
        {
            if (string.IsNullOrWhiteSpace(newVersion)) return false;
            if (string.IsNullOrWhiteSpace(currentVersion)) return true;

            bool currentIsPre = currentVersion.Contains("alpha") || currentVersion.Contains("beta");
            bool newIsPre = newVersion.Contains("alpha") || newVersion.Contains("beta");

            if (!currentIsPre && !newIsPre)
            {
                return Version.TryParse(currentVersion, out var curr) && Version.TryParse(newVersion, out var latest) && latest > curr;
            }

            var (baseCurr, labelCurr, betaNumCurr) = ExtractBetaVersion(currentVersion);
            var (baseNew, labelNew, betaNumNew) = ExtractBetaVersion(newVersion);

            if (Version.TryParse(baseCurr, out var baseC) && Version.TryParse(baseNew, out var baseN))
            {
                if (baseN > baseC) return true;
                if (baseN < baseC) return false;
            }

            if (string.Equals(labelCurr, "alpha", StringComparison.OrdinalIgnoreCase) && string.Equals(labelNew, "beta", StringComparison.OrdinalIgnoreCase))
                return true;

            return betaNumNew > betaNumCurr;
        }

        private static (string baseVersion, string? label, int betaNumber) ExtractBetaVersion(string version)
        {
            var match = Regex.Match(version, @"^(?<base>\d+\.\d+\.\d+)(?:-(?<label>alpha|beta)(?<number>\d+)?)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string baseVersion = match.Groups["base"].Value;
                string? label = match.Groups["label"].Success ? match.Groups["label"].Value : null;
                int betaNum = int.TryParse(match.Groups["number"].Value, out var n) ? n : 1;
                return (baseVersion, label, betaNum);
            }
            return (version, null, 1);
        }
    }

    // JSON Classes
    public class CurrentVersion
    {
        [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
        public string? Version { get; set; }

        [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
        public string? Location { get; set; }

        [JsonProperty("changelog", NullValueHandling = NullValueHandling.Ignore)]
        public string? Changelog { get; set; }
    }

    public class PreReleaseVersion
    {
        [JsonProperty("exists")]
        public bool Exists { get; set; }

        [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
        public string? Version { get; set; }

        [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
        public string? Location { get; set; }

        [JsonProperty("changelog", NullValueHandling = NullValueHandling.Ignore)]
        public string? Changelog { get; set; }
    }

    public class VersionInfo
    {
        [JsonProperty("Current")]
        public CurrentVersion? Current { get; set; }

        [JsonProperty("PreRelease")]
        public PreReleaseVersion? PreRelease { get; set; }
    }

    public class Root
    {
        [JsonProperty("Version")]
        public VersionInfo? Version { get; set; }
    }
}
