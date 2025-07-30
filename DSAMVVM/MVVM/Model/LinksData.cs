namespace DSAMVVM.MVVM.Model
{
    public class LinksData
    {
        public List<Link> CommonLinks { get; set; } = [];
        public List<TeamLinkGroup> TeamLinks { get; set; } = [];
    }

    public class TeamLinkGroup
    {
        public string Team { get; set; } = string.Empty;
        public List<Link> Links { get; set; } = [];
    }

    public class Link
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;
    }
}
