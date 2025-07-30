using Newtonsoft.Json;
using System.Collections.Generic;

namespace DSAMVVM.MVVM.Model
{
    public class DepartmentListWrapper
    {
        public List<Department>? DepartmentList { get; set; }
    }

    public class Department
    {
        public string Number { get; set; } = string.Empty;
        public bool SupportKnown { get; set; }
        public bool SplitSupport { get; set; }
        public List<Team>? Teams { get; set; }

        [JsonProperty("FileRepo")]
        public List<FileRepo>? FileRepos { get; set; }

        [JsonProperty("notes")]
        public string? Notes { get; set; }
    }

    public class Team
    {
        public string Name { get; set; } = string.Empty;
        public bool ServiceNow { get; set; }
    }

    public class FileRepo
    {
        public bool Exists { get; set; }
        public string? Location { get; set; }
    }
}
