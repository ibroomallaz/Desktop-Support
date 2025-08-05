using System.Collections.Generic;

namespace DSAMVVM.Core.Interfaces
{
    public record SearchContextDTO(string Query, string? Mode = null, Dictionary<string, object>? Options = null);
}
