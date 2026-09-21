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
        public SupportTeam Team { get; set; } = new();
        public StagedSupportTeamAction Action { get; set; } = StagedSupportTeamAction.AddOrUpdate;
    }
}
