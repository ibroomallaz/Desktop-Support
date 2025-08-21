using System.Collections.Generic;

namespace DSAMVVM.Core.Models
{
    public record SearchContextDTO(string Query, string? Mode = null, Dictionary<string, object>? Options = null);
}
