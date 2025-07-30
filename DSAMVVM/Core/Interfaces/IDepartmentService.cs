using DSAMVVM.MVVM.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Interfaces
{
    public interface IDepartment
    {
        string Number { get; }
        bool SupportKnown { get; }
        bool SplitSupport { get; }
        List<Team>? Teams { get; }
        List<FileRepo>? FileRepos { get; }
        string? Notes { get; }
    }

    public interface IDepartmentService
    {
        Task<IDepartment?> GetDepartmentAsync(string departmentNumber);
        Task<List<string>> GetTeamNamesAsync(string departmentNumber);
        Task<bool?> IsSupportKnownAsync(string departmentNumber);
        Task<string?> GetNotesAsync(string departmentNumber);
        Task<bool> HasFileRepoAsync(string departmentNumber);
        Task<FileRepo?> GetFileRepoAsync(string departmentNumber);
        Task PreCacheDataAsync();
        Task ReloadDataAsync();
    }
}