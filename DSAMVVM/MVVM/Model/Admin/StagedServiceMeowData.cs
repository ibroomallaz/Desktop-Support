using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.MVVM.Model.Admin
{
    public enum StagedServiceMeowAction
    {
        AddOrUpdate,
        Delete
    }

    public sealed class StagedServiceMeowData
    {
        public string? OriginalOwnerNetId { get; init; }
        public string OwnerNetId { get; init; } = string.Empty;
        public string OwnerName { get; init; } = string.Empty;
        public string OwnerTeam { get; init; } = string.Empty;
        public ServiceMeowPet Pet { get; init; } = new();
        public StagedServiceMeowAction Action { get; init; } = StagedServiceMeowAction.AddOrUpdate;
    }
}
