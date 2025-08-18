using DSAMVVM.MVVM.Model.Schemas;


namespace DSAMVVM.MVVM.Model.Data
{
    /*
      {
        "Meta": { "SchemaVersion": 1, "LastUpdatedUtc": "..." },
        "CommonLinks": [ { "Name": "...", "Description": "...", "URL": "..." } ],
        "TeamLinks": [
          { "Team": "...", "Links": [ { "Name": "...", "Description": "...", "URL": "..." } ] }
        ]
      }
    */

    public sealed class LinksData
    {
        public LinksMeta Meta { get; set; } = new();
        public List<Link> CommonLinks { get; set; } = [];
        public List<TeamLinkGroup> TeamLinks { get; set; } = [];
    }

    public sealed class TeamLinkGroup
    {
        public string Team { get; set; } = string.Empty;
        public List<Link> Links { get; set; } = [];
    }

    public sealed class Link
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;
    }
}
