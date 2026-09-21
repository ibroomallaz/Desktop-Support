using DSAMVVM.MVVM.Model.Admin;

namespace DSAMVVM.Core.Interfaces
{
    public interface IJsonExportService
    {
        Task<string> ExportDepartmentsJsonAsync(IEnumerable<StagedChange>? stagedChanges, string destinationFilePath);
        Task<string> ExportLinksJsonAsync(IEnumerable<StagedChange>? stagedChanges, string destinationFilePath);
        Task<IReadOnlyList<string>> ExportStagedChangesToFolderAsync(IEnumerable<StagedChange> stagedChanges, string destinationDirectory);
    }
}
