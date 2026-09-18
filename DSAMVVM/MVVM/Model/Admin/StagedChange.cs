using DSAMVVM.Core.Enums;

namespace DSAMVVM.MVVM.Model.Admin
{
    public class StagedChange
    {
        public AdminSection Section { get; init; }
        public string Key { get; init; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public object StagedData { get; init; } = null!;
        public DateTime Timestamp { get; init; } = DateTime.Now;
    }
}