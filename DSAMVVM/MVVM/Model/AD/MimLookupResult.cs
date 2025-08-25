namespace DSAMVVM.MVVM.Model.AD
{
    public sealed class MimLookupResult
    {
        public bool Exists { get; init; }
        public bool? Enabled { get; init; }
        public List<string> Groups { get; init; } = new();
        public string? Error { get; init; }
    }
}