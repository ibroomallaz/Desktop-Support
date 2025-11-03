using Newtonsoft.Json;

namespace DSAMVVM.MVVM.Model.Schemas
{
    public sealed class VersionManifest : JsonMetaBase
    {
        [JsonProperty("$schema", NullValueHandling = NullValueHandling.Ignore)]
        public string? Schema { get; set; }

        [JsonProperty(nameof(Version), NullValueHandling = NullValueHandling.Ignore)]
        public VersionInfo? Version { get; set; }

        [JsonProperty(nameof(Required), NullValueHandling = NullValueHandling.Ignore)]
        public RequiredUpdate? Required { get; set; }
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

    public sealed class RequiredUpdate
    {
        [JsonProperty("minVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string? MinVersion { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string? Message { get; set; }
    }
}
