using DSAMVVM.Core;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;


namespace DSAMVVM.MVVM.Model.utils
{
    public class VersionChecker
    {
        private static readonly HttpClient _client = new(); //Reusable client instance
        private static bool BetaCheck()
        {
            string version = Globals.g_AppVersion;
            return version.Contains("beta") || version.Contains("alpha");
        }
        //TODO: Replace old console alert methods
        public static async Task VersionCheck()
        {
            try
            {
                using HttpResponseMessage response = await _client.GetAsync(Globals.g_versionJSON);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var versionData = JsonConvert.DeserializeObject<Root>(json);

                if (versionData?.Version != null)
                {
                    NotifyUser(versionData.Version);
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine($"Message: {e.Message}");
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                Console.WriteLine("\nJSON Parsing Error!");
                Console.WriteLine($"Message: {e.Message}");
            }
        }

        private static void NotifyUser(VersionInfo versionInfo)
        {
            string installedVersion = Globals.g_AppVersion;
            bool isBetaUser = BetaCheck();

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            bool isStableUpdateAvailable = IsNewerVersion(installedVersion, versionInfo.Current.Version);
            bool isBetaUpdateAvailable = versionInfo.PreRelease.Exists && IsNewerVersion(installedVersion, versionInfo.PreRelease.Version);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            bool isBetaHigherThanStable = IsNewerVersion(versionInfo.Current.Version, versionInfo.PreRelease.Version);

            // Notify about stable update (applies to all users)
            if (isStableUpdateAvailable && !isBetaUser)
            {
#pragma warning disable CS8604 // Possible null reference argument.
                PromptUpdate("Update Available", versionInfo.Current.Version, installedVersion, versionInfo.Current.Location, versionInfo.Current.Changelog, false);
            }

            if (isBetaUser)
            {
                if (isBetaUpdateAvailable)
                {
                    // Only update beta if a new beta version exists
                    PromptUpdate("PreRelease Update Available", versionInfo.PreRelease.Version, installedVersion, versionInfo.PreRelease.Location, versionInfo.PreRelease.Changelog, true);
                }
                else if (!isBetaHigherThanStable && isStableUpdateAvailable)
                {
                    // If beta version is not higher than stable and stable is newer or equal, update to stable
                    PromptUpdate("Stable Update Recommended", versionInfo.Current.Version, installedVersion, versionInfo.Current.Location, versionInfo.Current.Changelog, false);
#pragma warning restore CS8604 // Possible null reference argument.
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
                    isBeta ? MessageBoxImage.Information : MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.OK)
                {
                    HTTP.OpenURL(location);
                }
            });
        }

        private static bool IsNewerVersion(string? currentVersion, string? newVersion)
        {
            if (string.IsNullOrEmpty(newVersion)) return false;
            if (string.IsNullOrEmpty(currentVersion)) return true;

            bool currentIsPreRelease = currentVersion.Contains("alpha") || currentVersion.Contains("beta");
            bool newIsPreRelease = newVersion.Contains("alpha") || newVersion.Contains("beta");

            if (!currentIsPreRelease && !newIsPreRelease)
            {
                return Version.TryParse(currentVersion, out var current) &&
                       Version.TryParse(newVersion, out var latest) &&
                       latest > current;
            }

            var (baseCurrent, labelCurrent, currentBetaNumber) = ExtractBetaVersion(currentVersion);
            var (baseNew, labelNew, newBetaNumber) = ExtractBetaVersion(newVersion);

            if (Version.TryParse(baseCurrent, out var currentBase) && Version.TryParse(baseNew, out var newBase))
            {
                if (newBase > currentBase)
                    return true;
                if (newBase < currentBase)
                    return false;
            }

            if (string.Equals(labelCurrent, "alpha", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(labelNew, "beta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // If both versions have the same label or no special ordering is required, compare the beta numbers.
            return newBetaNumber > currentBetaNumber;
        }


        private static (string baseVersion, string? label, int betaNumber) ExtractBetaVersion(string version)
        {
            var match = Regex.Match(version, @"^(?<base>\d+\.\d+\.\d+)(?:-(?<label>alpha|beta)(?<number>\d+)?)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string baseVersion = match.Groups["base"].Value;
                string? label = match.Groups["label"].Success ? match.Groups["label"].Value : null;
                string number = match.Groups["number"].Value;
                int betaNumber = string.IsNullOrEmpty(number) ? 1 : int.Parse(number);
                return (baseVersion, label, betaNumber);
            }
            return (version, null, 1); // Fallback if regex fails
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
