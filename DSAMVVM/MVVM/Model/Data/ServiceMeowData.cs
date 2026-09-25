using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace DSAMVVM.MVVM.Model.Data
{
    public sealed class ServiceMeowData
    {
        public ServiceMeowMeta Meta { get; set; } = new();
        public List<ServiceMeowOwner> Owners { get; set; } = [];

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            foreach (var owner in Owners)
            {
                foreach (var pet in owner.Pets)
                {
                    pet.Owner = owner;
                }
            }
        }

        // Flattened list for rotation indexing, random selection, and search
        [JsonIgnore]
        public IReadOnlyList<ServiceMeowPet> AllPets =>
            Owners.SelectMany(o => o.Pets).ToList();
    }

    public sealed class ServiceMeowOwner
    {
        // NetID uniquely identifies staff at the top to avoid name/initial collisions
        public string NetId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public List<ServiceMeowPet> Pets { get; set; } = [];

        [JsonIgnore]
        public string OwnerDisplay => string.IsNullOrWhiteSpace(Team)
            ? Name
            : $"{Name} · {Team}";
    }

    public sealed class ServiceMeowPet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Species { get; set; } = "Cat";
        public string Blurb { get; set; } = string.Empty;
        public List<string> Images { get; set; } = [];

        [JsonIgnore]
        public ServiceMeowOwner? Owner { get; set; }

        // Primary image compatibility getter for existing view bindings
        [JsonIgnore]
        public string? ImageUrl => Images.Count > 0 ? Images[0] : null;

        [JsonIgnore]
        public string OwnerDisplay => Owner?.OwnerDisplay ?? string.Empty;
    }
}
