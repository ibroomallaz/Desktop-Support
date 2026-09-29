namespace DSAMVVM.MVVM.Model.Config.Paths
{
    public sealed class DataLocation
    {
        public bool UseCustomSource { get; set; }
        public string Source { get; set; } = "web";
        public string Uri { get; set; } = string.Empty;
        public string? FallbackFile { get; set; }

        public void Normalize()
        {
            if (!Source.Equals("file", StringComparison.OrdinalIgnoreCase) &&
                !Source.Equals("web", StringComparison.OrdinalIgnoreCase))
            {
                Source = "web";
            }

            if (string.IsNullOrWhiteSpace(FallbackFile)) FallbackFile = null;
            if (UseCustomSource && string.IsNullOrWhiteSpace(Uri)) UseCustomSource = false;
        }
    }
}