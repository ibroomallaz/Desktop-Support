using DSAMVVM.MVVM.Model.Admin;
using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.Core.Interfaces
{
    public interface IAdminService
    {
        // Department operations
        Task<DepartmentListWrapper> LoadDepartmentsAsync();
        Task<IReadOnlyList<string>> GetAvailableDepartmentTeamsAsync();
        Task<Department?> FindDepartmentAsync(string departmentId);

        // Support Team operations
        Task<IReadOnlyList<SupportTeam>> LoadSupportTeamsAsync();
        Task<SupportTeam?> FindSupportTeamAsync(string query);

        // Links operations
        Task<LinksData> LoadLinksDataAsync();
        Task<IReadOnlyList<string>> GetAvailableLinkTeamsAsync();
        Task<(Link? Link, bool IsCommon, string? Team)> FindLinkAsync(string query);

        // Staging helpers
        DepartmentListWrapper ApplyDepartmentChanges(DepartmentListWrapper wrapper, IEnumerable<StagedChange> stagedChanges);
        LinksData ApplyLinkChanges(LinksData linksData, IEnumerable<StagedChange> stagedChanges);

        // Persistence
        Task SaveStagedChangesAsync(IEnumerable<StagedChange> stagedChanges);
    }
}
