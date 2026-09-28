using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.MVVM.Model.Admin
{
    public enum StagedSupportTeamAction
    {
        AddOrUpdate,
        Delete
    }

    public sealed class StagedSupportTeamData
    {
        public SupportTeam Team { get; init; } = new();
        public StagedSupportTeamAction Action { get; init; } = StagedSupportTeamAction.AddOrUpdate;
    }
}
