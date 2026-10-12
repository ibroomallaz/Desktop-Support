using Newtonsoft.Json;

namespace DSAMVVM.MVVM.Model.Schemas
{
    public interface IJsonMeta
    {
        int SchemaVersion { get; set; }
        DateTime? LastUpdatedUtc { get; set; }
    }
    public abstract class JsonMetaBase : IJsonMeta
    {
        public int SchemaVersion { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastUpdatedUtc { get; set; }

        public void Normalize()
        {
            LastUpdatedUtc ??= DateTime.UtcNow;
        }
    }
}
