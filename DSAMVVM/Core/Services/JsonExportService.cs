using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model.Admin;
using Newtonsoft.Json;
using System.IO;

namespace DSAMVVM.Core.Services
{
    public class JsonExportService(IAdminService adminService) : IJsonExportService
    {
        private const string Tag = "JsonExportService";
        private readonly IAdminService _adminService = adminService ?? throw new ArgumentNullException(nameof(adminService));

        public async Task<string> ExportDepartmentsJsonAsync(IEnumerable<StagedChange>? stagedChanges, string destinationFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

            var wrapper = await _adminService.LoadDepartmentsAsync();

            if (stagedChanges != null)
            {
                wrapper = _adminService.ApplyDepartmentChanges(wrapper, stagedChanges);
            }

            string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);

            var dir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(destinationFilePath, json);
            Log.Info(Tag, $"Exported departments.json to: {destinationFilePath}");

            return destinationFilePath;
        }

        public async Task<string> ExportLinksJsonAsync(IEnumerable<StagedChange>? stagedChanges, string destinationFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

            var linksData = await _adminService.LoadLinksDataAsync();

            if (stagedChanges != null)
            {
                linksData = _adminService.ApplyLinkChanges(linksData, stagedChanges);
            }

            string json = JsonConvert.SerializeObject(linksData, Formatting.Indented);

            var dir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(destinationFilePath, json);
            Log.Info(Tag, $"Exported links.json to: {destinationFilePath}");

            return destinationFilePath;
        }

        public async Task<IReadOnlyList<string>> ExportStagedChangesToFolderAsync(IEnumerable<StagedChange> stagedChanges, string destinationDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
            ArgumentNullException.ThrowIfNull(stagedChanges);

            Directory.CreateDirectory(destinationDirectory);

            var changesList = stagedChanges.ToList();
            bool hasDeptChanges = changesList.Any(c => c.Section == AdminSection.Department || c.Section == AdminSection.SupportTeam);
            bool hasLinkChanges = changesList.Any(c => c.Section == AdminSection.Links);

            // If no changes staged at all, export both as baselines
            if (!hasDeptChanges && !hasLinkChanges)
            {
                hasDeptChanges = true;
                hasLinkChanges = true;
            }

            var exportedFiles = new List<string>();

            if (hasDeptChanges)
            {
                string deptPath = Path.Combine(destinationDirectory, "departments.json");
                await ExportDepartmentsJsonAsync(changesList, deptPath);
                exportedFiles.Add(deptPath);
            }

            if (hasLinkChanges)
            {
                string linksPath = Path.Combine(destinationDirectory, "links.json");
                await ExportLinksJsonAsync(changesList, linksPath);
                exportedFiles.Add(linksPath);
            }

            return exportedFiles;
        }
    }
}
