using DSAMVVM.Core.Enums;

namespace DSAMVVM.Core.Models
{
    /// <summary>
    /// Records an individual executed search query and target view category for history tracking.
    /// </summary>
    public sealed record SearchHistoryEntry(string Query, SearchTarget Target, DateTime Timestamp);
}
