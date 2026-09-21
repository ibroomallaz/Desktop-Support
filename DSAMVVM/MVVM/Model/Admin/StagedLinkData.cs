using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.MVVM.Model.Admin
{
    public enum StagedLinkAction
    {
        AddOrUpdate,
        Delete
    }

    public sealed class StagedLinkData
    {
        public bool IsCommon { get; set; } = true;
        public string? Team { get; set; }
        public Link Link { get; set; } = new();
        public StagedLinkAction Action { get; set; } = StagedLinkAction.AddOrUpdate;
    }
}
