using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;


namespace DSAMVVM.MVVM.Model.Data
{
    /*
      {
       "Meta": { "SchemaVersion": 2, "LastUpdatedUtc": "..." },
       "DepartmentList": [ { "Number": "...", "SupportKnown": true, "Team": "...", "Notes": "..." } ]
      }
    */
    public sealed class DepartmentListWrapper
    {
        public DepartmentMeta Meta { get; set; } = new();

        [JsonProperty("DepartmentList")]
        public List<Department> DepartmentList { get; set; } = new();
    }

    public sealed class Department
    {
        public string Number { get; set; } = string.Empty;
        public bool SupportKnown { get; set; }
        public string? Team { get; set; }
        public string? Notes { get; set; }
        public string? FileRepoPath { get; set; }
    }
}
