using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.Core.Interfaces
{
    public interface IDepartment
    {
        string Number { get; }
        bool SupportKnown { get; }
        string? Team { get; }
        string? Notes { get; }
        string? FileRepoPath { get; }

        string? ManagerName { get; }
        string? ManagerNetId { get; }
        string? SupportPhoneNumber { get; }
        List<SupportedDivs>? SupportedDivisions { get; }
    }

    public interface IDepartmentService
    {
        Task<IDepartment?> GetDepartmentAsync(string departmentNumber);

        Task<SupportTeam?> GetSupportTeamAsync(string teamName);

        Task<string?> GetTeamAsync(string departmentNumber);
        Task<bool?> IsSupportKnownAsync(string departmentNumber);
        Task<string?> GetNotesAsync(string departmentNumber);
        Task<string?> GetFileRepoPathAsync(string departmentNumber);
        Task<IEnumerable<SupportTeam>> GetTeamsByDivisionAsync(string divAbbrev);
        Task PreCacheDataAsync();
        Task ReloadDataAsync();
    }
}