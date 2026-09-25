using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;

namespace DSAMVVM.MVVM.Model.Data
{
    /*
      {
        "Meta": { "SchemaVersion": 1, "LastUpdatedUtc": "..." },
        "Pets": [
          {
            "Name": "Nimbus",
            "Title": "Chief Packet Sniffer",
            "Breed": "British Shorthair",
            "Species": "Cat",
            "Owner": "Dave T.",
            "OwnerTeam": "Network Operations",
            "Blurb": "Discovered a loose patch cable by chewing on the boot.",
            "ImageUrl": null
          }
        ]
      }
    */
    public sealed class ServiceMeowData
    {
        public ServiceMeowMeta Meta { get; set; } = new();
        public List<ServiceMeowPet> Pets { get; set; } = [];
    }

    public sealed class ServiceMeowPet
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Species { get; set; } = "Cat";
        public string Owner { get; set; } = string.Empty;
        public string OwnerTeam { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }

        [JsonIgnore]
        public string OwnerDisplay => string.IsNullOrWhiteSpace(OwnerTeam)
            ? Owner
            : $"{Owner} · {OwnerTeam}";
    }
}
