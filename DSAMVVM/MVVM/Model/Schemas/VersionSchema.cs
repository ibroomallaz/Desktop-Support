using Newtonsoft.Json;

namespace DSAMVVM.MVVM.Model.Schemas
{
    // Top-level JSON object (rename from "Root" for clarity)
    public sealed class VersionResponse
    {
        [JsonProperty(nameof(Version))]
        public VersionInfo? Version { get; set; }
    }

    public sealed class VersionInfo
    {
        [JsonProperty(nameof(Current))]
        public CurrentVersion? Current { get; set; }

        [JsonProperty(nameof(PreRelease))]
        public PreReleaseVersion? PreRelease { get; set; }
    }

    public sealed class CurrentVersion
    {
        [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
        public string? Version { get; set; }

        [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
        public string? Location { get; set; }

        [JsonProperty("changelog", NullValueHandling = NullValueHandling.Ignore)]
        public string? Changelog { get; set; }
    }

    public sealed class PreReleaseVersion
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
}
