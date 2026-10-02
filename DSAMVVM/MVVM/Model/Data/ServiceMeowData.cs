using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;

namespace DSAMVVM.MVVM.Model.Data
{
    public class ServiceMeowData
    {
        public int SchemaVersion { get; set; } = 1;
        public ServiceMeowMeta Meta { get; init; } = new();
        public List<ServiceMeowOwner> Owners { get; init; } = [];

        [JsonIgnore]
        public List<ServiceMeowPet> AllPets =>
            Owners.SelectMany(o => o.Pets).ToList();
    }

    public class ServiceMeowOwner
    {
        public string NetId { get; init; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public List<ServiceMeowPet> Pets { get; init; } = [];

        [JsonIgnore]
        public string OwnerDisplay =>
            !string.IsNullOrWhiteSpace(Team) ? $"{Name} · {Team}" : Name;
    }

    public class ServiceMeowPet
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Species { get; set; } = "Cat";
        public string Blurb { get; set; } = string.Empty;
        public List<string> Images { get; set; } = [];

        [JsonIgnore]
        public ServiceMeowOwner? Owner { get; set; }

        [JsonIgnore]
        public List<string> ValidImages =>
            Images.Where(img =>
            {
                if (string.IsNullOrWhiteSpace(img)) return false;
                var trimmed = img.Trim();
                return !string.Equals(trimmed, "TBD", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(trimmed, "TDB", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(trimmed, "NONE", StringComparison.OrdinalIgnoreCase);
            }).Select(img => img.Trim()).ToList();

        // Primary image compatibility getter for existing view bindings
        [JsonIgnore]
        public string? ImageUrl => ValidImages.Count > 0 ? ValidImages[0] : null;

        [JsonIgnore]
        public string BreedOrSpecies => !string.IsNullOrWhiteSpace(Breed) ? Breed : Species;

        [JsonIgnore]
        public string OwnerDisplay => Owner?.OwnerDisplay ?? string.Empty;
    }
}
