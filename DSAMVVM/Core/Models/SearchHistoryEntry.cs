using DSAMVVM.Core.Enums;

namespace DSAMVVM.Core.Models
{
    public sealed record SearchHistoryEntry(string Query, SearchTarget Target, DateTime Timestamp);
}
