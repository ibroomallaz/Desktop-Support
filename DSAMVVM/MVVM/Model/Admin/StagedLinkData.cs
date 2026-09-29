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
        public bool IsCommon { get; init; } = true;
        public string? Team { get; init; }
        public Link Link { get; init; } = new();
        public StagedLinkAction Action { get; init; } = StagedLinkAction.AddOrUpdate;
    }
}
