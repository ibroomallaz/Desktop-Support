﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

public class VersionChecker
{
    private static bool BetaCheck()
    {
        string version = Application.ProductVersion.ToLower();
        return version.Contains("beta") || version.Contains("alpha");
    }

    public static async Task VersionCheck()
    {
        try
        {
            Console.WriteLine("Checking Version...");
            HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(Globals.g_versionJSON);
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
        catch (JsonException e)
        {
            Console.WriteLine("\nJSON Parsing Error!");
            Console.WriteLine($"Message: {e.Message}");
        }
    }

    private static void NotifyUser(VersionInfo versionInfo)
    {
        string installedVersion = Application.ProductVersion;
        bool isBetaUser = BetaCheck();

        bool isStableUpdateAvailable = IsNewerVersion(installedVersion, versionInfo.Current.Version);
        bool isBetaUpdateAvailable = versionInfo.PreRelease.Exists && IsNewerVersion(installedVersion, versionInfo.PreRelease.Version);
        bool isBetaHigherThanStable = IsNewerVersion(versionInfo.Current.Version, versionInfo.PreRelease.Version);

        if (isStableUpdateAvailable && !isBetaUser)
        {
            PromptUpdate("Update Available", versionInfo.Current.Version, installedVersion, versionInfo.Current.Location, versionInfo.Current.Changelog, false);
        }


        if (isBetaUser)
        {
            if (isBetaUpdateAvailable)
            {
                // Only update beta if a new beta version exists
                PromptUpdate("Beta Update Available", versionInfo.PreRelease.Version, installedVersion, versionInfo.PreRelease.Location, versionInfo.PreRelease.Changelog, true);
            }
            else if (!isBetaHigherThanStable && isStableUpdateAvailable)
            {
                // If beta version is not higher than stable and stable is newer or equal, update to stable
                PromptUpdate("Stable Update Recommended", versionInfo.Current.Version, installedVersion, versionInfo.Current.Location, versionInfo.Current.Changelog, false);
            }
        }
    }

    private static void PromptUpdate(string title, string newVersion, string installedVersion, string? location, string? changelog, bool isBeta)
    {
        location ??= Globals.g_sharepointHome; // Fallback URL
        changelog ??= "No details provided.";

        var result = MessageBox.Show(
            $"{title}\n\nA new version ({newVersion}) is available.\n\nCurrent version: {installedVersion}\n\nChanges:\n{changelog}\n\nWould you like to update?",
            title,
            MessageBoxButtons.OKCancel,
            isBeta ? MessageBoxIcon.Information : MessageBoxIcon.Exclamation
        );

        if (result == DialogResult.OK)
        {
            HTTP.OpenURL(location);
        }
    }

    private static bool IsNewerVersion(string? currentVersion, string? newVersion)
    {
        if (string.IsNullOrEmpty(newVersion)) return false; // No new version available
        if (string.IsNullOrEmpty(currentVersion)) return true; // If current is null, assume update is available

        return Version.TryParse(currentVersion, out var current) &&
               Version.TryParse(newVersion, out var latest) &&
               latest > current;
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
    public CurrentVersion Current { get; set; }

    [JsonProperty("PreRelease")]
    public PreReleaseVersion PreRelease { get; set; }
}

public class Root
{
    [JsonProperty("Version")]
    public VersionInfo Version { get; set; }
}
