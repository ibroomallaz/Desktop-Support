using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DSAMVVM.Core
{
    public class VersionCheckResult
    {
        public VersionInfo? Info { get; set; }
        public string? Error { get; set; }
        public bool Success => Info != null;
    }

    public static partial class VersionChecker
    {
        public static async Task<VersionCheckResult> CheckVersionAsync(string versionJsonUrl)
        {
            try
            {
                using HttpClient client = new();
                string json = await client.GetStringAsync(versionJsonUrl);
                var data = JsonConvert.DeserializeObject<Root>(json);
                return new VersionCheckResult { Info = data?.Version };
            }
            catch (Exception ex)
            {
                return new VersionCheckResult { Error = ex.Message };
            }
        }

        public static bool IsNewerVersion(string? currentVersion, string? newVersion)
        {
            if (string.IsNullOrWhiteSpace(newVersion)) return false;
            if (string.IsNullOrWhiteSpace(currentVersion)) return true;

            bool currentIsPre = currentVersion.Contains("alpha") || currentVersion.Contains("beta");
            bool newIsPre = newVersion.Contains("alpha") || newVersion.Contains("beta");

            if (!currentIsPre && !newIsPre)
            {
                return Version.TryParse(currentVersion, out var curr) &&
                       Version.TryParse(newVersion, out var latest) &&
                       latest > curr;
            }

            var (baseCurr, labelCurr, betaNumCurr) = ExtractBetaVersion(currentVersion);
            var (baseNew, labelNew, betaNumNew) = ExtractBetaVersion(newVersion);

            if (Version.TryParse(baseCurr, out var baseC) &&
                Version.TryParse(baseNew, out var baseN))
            {
                if (baseN > baseC) return true;
                if (baseN < baseC) return false;
            }

            if (string.Equals(labelCurr, "alpha", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(labelNew, "beta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return betaNumNew > betaNumCurr;
        }

        public static (string baseVersion, string? label, int betaNumber) ExtractBetaVersion(string version)
        {
            var match = VersionRegex().Match(version);
            if (match.Success)
            {
                string baseVersion = match.Groups["base"].Value;
                string? label = match.Groups["label"].Success ? match.Groups["label"].Value : null;
                int betaNum = int.TryParse(match.Groups["number"].Value, out var n) ? n : 1;
                return (baseVersion, label, betaNum);
            }
            return (version, null, 1);
        }

        [GeneratedRegex(@"^(?<base>\d+\.\d+\.\d+)(?:-(?<label>alpha|beta)(?<number>\d+)?)?$", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex VersionRegex();
    }

    // JSON Models
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
        [JsonProperty(nameof(Current))]
        public CurrentVersion? Current { get; set; }

        [JsonProperty(nameof(PreRelease))]
        public PreReleaseVersion? PreRelease { get; set; }
    }

    public class Root
    {
        [JsonProperty(nameof(Version))]
        public VersionInfo? Version { get; set; }
    }
}
