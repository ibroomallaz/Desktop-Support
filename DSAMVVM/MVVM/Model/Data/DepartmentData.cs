using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;

namespace DSAMVVM.MVVM.Model.Data
{
    /*
      {
       "Meta": { "SchemaVersion": 2, "LastUpdatedUtc": "..." },
       "DepartmentList": [ { "Number": "...", "SupportKnown": true, "Team": "...", "Notes": "..." } ],
       "SupportTeams": [ ... ]
      }
    */
    public sealed class DepartmentListWrapper
    {
        public DepartmentMeta Meta { get; set; } = new();

        [JsonProperty(nameof(DepartmentList))]
        public List<Department> DepartmentList { get; set; } = [];

        [JsonProperty(nameof(SupportTeams))]
        public List<SupportTeam> SupportTeams { get; set; } = [];
    }

    public sealed class Department
    {
        public string Number { get; init; } = string.Empty;
        public bool SupportKnown { get; set; }
        public string? Team { get; set; }
        public string? Notes { get; set; }
        public string? FileRepoPath { get; set; }
    }

    public sealed class SupportTeam
    {
        [JsonProperty(nameof(SupportTeamName))]
        public string SupportTeamName { get; set; } = string.Empty;

        public string ManagerName { get; set; } = string.Empty;

        [JsonProperty(nameof(ManagerNetID))]
        public string ManagerNetID { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        [JsonProperty("SupportedDivs")]
        public List<SupportedDivs> SupportedDivisions { get; set; } = [];
    }

    public sealed class SupportedDivs
    {
        public string DivAbbrev { get; init; } = string.Empty;
        public string DivFullName { get; init; } = string.Empty;
    }
}