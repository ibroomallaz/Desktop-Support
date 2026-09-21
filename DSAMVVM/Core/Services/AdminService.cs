using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Admin;
using DSAMVVM.MVVM.Model.Data;
using Newtonsoft.Json;
using System.IO;

namespace DSAMVVM.Core.Services
{
    public class AdminService(IDepartmentService deptService, ILinksService linksService) : IAdminService
    {
        private const string Tag = "AdminService";

        private readonly IDepartmentService _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
        private readonly ILinksService _linksService = linksService ?? throw new ArgumentNullException(nameof(linksService));

        #region Paths

        private static string DepartmentTargetPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "departments.json");

        private static string LinksTargetPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "links.json");

        #endregion

        #region Department Operations

        public async Task<DepartmentListWrapper> LoadDepartmentsAsync()
        {
            try
            {
                string targetPath = DepartmentTargetPath;
                if (!File.Exists(targetPath) && File.Exists(Globals.g_DepartmentCachePath))
                {
                    targetPath = Globals.g_DepartmentCachePath;
                }

                if (File.Exists(targetPath))
                {
                    string json = await File.ReadAllTextAsync(targetPath);
                    var wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(json);
                    if (wrapper != null) return wrapper;
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"Failed to load departments from disk: {ex.Message}");
            }

            return new DepartmentListWrapper();
        }

        public async Task<IReadOnlyList<string>> GetAvailableDepartmentTeamsAsync()
        {
            var wrapper = await LoadDepartmentsAsync();
            var teamSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (wrapper.SupportTeams != null)
            {
                foreach (var st in wrapper.SupportTeams)
                {
                    if (!string.IsNullOrWhiteSpace(st.SupportTeamName))
                        teamSet.Add(st.SupportTeamName.Trim());
                }
            }

            return teamSet.OrderBy(t => t).ToList();
        }

        public async Task<Department?> FindDepartmentAsync(string departmentId)
        {
            if (string.IsNullOrWhiteSpace(departmentId)) return null;

            var wrapper = await LoadDepartmentsAsync();
            var local = wrapper.DepartmentList.FirstOrDefault(d =>
                string.Equals(d.Number, departmentId.Trim(), StringComparison.OrdinalIgnoreCase));

            if (local != null) return local;

            // Fallback to department service query
            var dept = await _deptService.GetDepartmentAsync(departmentId);
            if (dept != null)
            {
                return new Department
                {
                    Number = dept.Number,
                    SupportKnown = dept.SupportKnown,
                    Team = dept.Team,
                    Notes = dept.Notes,
                    FileRepoPath = dept.FileRepoPath
                };
            }

            return null;
        }

        #endregion

        #region Support Team Operations

        public async Task<IReadOnlyList<SupportTeam>> LoadSupportTeamsAsync()
        {
            var wrapper = await LoadDepartmentsAsync();
            return (wrapper.SupportTeams ?? []).OrderBy(t => t.SupportTeamName).ToList();
        }

        public async Task<SupportTeam?> FindSupportTeamAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            var trimmed = query.Trim();
            var wrapper = await LoadDepartmentsAsync();
            var teams = wrapper.SupportTeams ?? [];

            // 1. Exact match by SupportTeamName or ManagerNetID
            var exact = teams.FirstOrDefault(t =>
                string.Equals(t.SupportTeamName, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.ManagerNetID, trimmed, StringComparison.OrdinalIgnoreCase));

            if (exact != null) return exact;

            // 2. Partial match by SupportTeamName or ManagerName
            var partial = teams.FirstOrDefault(t =>
                t.SupportTeamName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                t.ManagerName.Contains(trimmed, StringComparison.OrdinalIgnoreCase));

            if (partial != null) return partial;

            // 3. Fallback to department service query
            return await _deptService.GetSupportTeamAsync(trimmed);
        }

        #endregion

        #region Links Operations

        public async Task<LinksData> LoadLinksDataAsync()
        {
            try
            {
                string targetPath = LinksTargetPath;
                if (!File.Exists(targetPath) && File.Exists(Globals.g_LinksCachePath))
                {
                    targetPath = Globals.g_LinksCachePath;
                }

                if (File.Exists(targetPath))
                {
                    string json = await File.ReadAllTextAsync(targetPath);
                    var data = JsonConvert.DeserializeObject<LinksData>(json);
                    if (data != null) return data;
                }

                if (_linksService.GetCachedLinksData() is { } cached)
                {
                    return cached;
                }

                var loaded = await _linksService.LoadLinksDataAsync();
                if (loaded != null) return loaded;
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"Failed to load links data: {ex.Message}");
            }

            return new LinksData();
        }

        public async Task<IReadOnlyList<string>> GetAvailableLinkTeamsAsync()
        {
            var teamSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var links = await LoadLinksDataAsync();
            foreach (var group in links.TeamLinks)
            {
                if (!string.IsNullOrWhiteSpace(group.Team))
                    teamSet.Add(group.Team.Trim());
            }

            var deptTeams = await GetAvailableDepartmentTeamsAsync();
            foreach (var team in deptTeams)
            {
                if (!string.IsNullOrWhiteSpace(team))
                    teamSet.Add(team.Trim());
            }

            return teamSet.OrderBy(t => t).ToList();
        }

        public async Task<(Link? Link, bool IsCommon, string? Team)> FindLinkAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return (null, true, null);

            var trimmed = query.Trim();
            var links = await LoadLinksDataAsync();

            // 1. Search CommonLinks (exact match first, then partial)
            var commonMatch = links.CommonLinks.FirstOrDefault(l =>
                string.Equals(l.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                ?? links.CommonLinks.FirstOrDefault(l =>
                    l.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase));

            if (commonMatch != null)
            {
                return (commonMatch, true, null);
            }

            // 2. Search TeamLinks (exact match first, then partial)
            foreach (var group in links.TeamLinks)
            {
                var match = group.Links.FirstOrDefault(l =>
                    string.Equals(l.Name, trimmed, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return (match, false, group.Team);
                }
            }

            foreach (var group in links.TeamLinks)
            {
                var match = group.Links.FirstOrDefault(l =>
                    l.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return (match, false, group.Team);
                }
            }

            return (null, true, null);
        }

        #endregion

        #region Staging Helpers

        public DepartmentListWrapper ApplyDepartmentChanges(DepartmentListWrapper wrapper, IEnumerable<StagedChange> stagedChanges)
        {
            ArgumentNullException.ThrowIfNull(wrapper);
            ArgumentNullException.ThrowIfNull(stagedChanges);

            var deptChanges = stagedChanges.Where(c => c.Section == AdminSection.Department).ToList();
            var supportTeamChanges = stagedChanges.Where(c => c.Section == AdminSection.SupportTeam).ToList();

            // 1. Apply Department changes
            foreach (var change in deptChanges)
            {
                if (change.StagedData is Department stagedDept)
                {
                    var existing = wrapper.DepartmentList.FirstOrDefault(d =>
                        string.Equals(d.Number, stagedDept.Number, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        existing.SupportKnown = stagedDept.SupportKnown;
                        existing.Team = stagedDept.Team;
                        existing.Notes = stagedDept.Notes;
                        existing.FileRepoPath = stagedDept.FileRepoPath;
                    }
                    else
                    {
                        wrapper.DepartmentList.Add(stagedDept);
                    }
                }
            }

            // 2. Apply Support Team changes
            foreach (var change in supportTeamChanges)
            {
                if (change.StagedData is StagedSupportTeamData stagedTeam)
                {
                    var existing = wrapper.SupportTeams.FirstOrDefault(t =>
                        string.Equals(t.SupportTeamName, stagedTeam.Team.SupportTeamName, StringComparison.OrdinalIgnoreCase));

                    if (stagedTeam.Action == StagedSupportTeamAction.Delete)
                    {
                        if (existing != null) wrapper.SupportTeams.Remove(existing);
                    }
                    else
                    {
                        if (existing != null)
                        {
                            existing.ManagerName = stagedTeam.Team.ManagerName;
                            existing.ManagerNetID = stagedTeam.Team.ManagerNetID;
                            existing.PhoneNumber = stagedTeam.Team.PhoneNumber;
                            existing.SupportedDivisions = stagedTeam.Team.SupportedDivisions;
                        }
                        else
                        {
                            wrapper.SupportTeams.Add(stagedTeam.Team);
                        }
                    }
                }
            }

            wrapper.Meta ??= new();
            wrapper.Meta.LastUpdatedUtc = DateTime.UtcNow;

            return wrapper;
        }

        public LinksData ApplyLinkChanges(LinksData linksData, IEnumerable<StagedChange> stagedChanges)
        {
            ArgumentNullException.ThrowIfNull(linksData);
            ArgumentNullException.ThrowIfNull(stagedChanges);

            var linkChanges = stagedChanges.Where(c => c.Section == AdminSection.Links).ToList();

            foreach (var change in linkChanges)
            {
                if (change.StagedData is StagedLinkData staged)
                {
                    if (staged.IsCommon)
                    {
                        var existing = linksData.CommonLinks.FirstOrDefault(l =>
                            string.Equals(l.Name, staged.Link.Name, StringComparison.OrdinalIgnoreCase));

                        if (staged.Action == StagedLinkAction.Delete)
                        {
                            if (existing != null) linksData.CommonLinks.Remove(existing);
                        }
                        else
                        {
                            if (existing != null)
                            {
                                existing.Name = staged.Link.Name;
                                existing.URL = staged.Link.URL;
                                existing.Description = staged.Link.Description;
                            }
                            else
                            {
                                linksData.CommonLinks.Add(staged.Link);
                            }
                        }
                    }
                    else
                    {
                        string team = (staged.Team ?? string.Empty).Trim();
                        var group = linksData.TeamLinks.FirstOrDefault(g =>
                            string.Equals(g.Team, team, StringComparison.OrdinalIgnoreCase));

                        if (group == null && staged.Action != StagedLinkAction.Delete)
                        {
                            group = new TeamLinkGroup { Team = team, Links = [] };
                            linksData.TeamLinks.Add(group);
                        }

                        if (group != null)
                        {
                            var existing = group.Links.FirstOrDefault(l =>
                                string.Equals(l.Name, staged.Link.Name, StringComparison.OrdinalIgnoreCase));

                            if (staged.Action == StagedLinkAction.Delete)
                            {
                                if (existing != null) group.Links.Remove(existing);
                            }
                            else
                            {
                                if (existing != null)
                                {
                                    existing.Name = staged.Link.Name;
                                    existing.URL = staged.Link.URL;
                                    existing.Description = staged.Link.Description;
                                }
                                else
                                {
                                    group.Links.Add(staged.Link);
                                }
                            }
                        }
                    }
                }
            }

            linksData.Meta ??= new();
            linksData.Meta.SchemaVersion = Globals.g_LinkJSONSchema;
            linksData.Meta.LastUpdatedUtc = DateTime.UtcNow;

            return linksData;
        }

        #endregion

        #region Persistence

        public async Task SaveStagedChangesAsync(IEnumerable<StagedChange> stagedChanges)
        {
            ArgumentNullException.ThrowIfNull(stagedChanges);

            var deptChanges = stagedChanges.Where(c => c.Section == AdminSection.Department).ToList();
            var supportTeamChanges = stagedChanges.Where(c => c.Section == AdminSection.SupportTeam).ToList();
            var linkChanges = stagedChanges.Where(c => c.Section == AdminSection.Links).ToList();

            if (deptChanges.Count == 0 && supportTeamChanges.Count == 0 && linkChanges.Count == 0) return;

            // 1. Persist Department & Support Team changes to departments.json
            if (deptChanges.Count > 0 || supportTeamChanges.Count > 0)
            {
                var wrapper = await LoadDepartmentsAsync();
                ApplyDepartmentChanges(wrapper, stagedChanges);

                string deptJson = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
                await File.WriteAllTextAsync(DepartmentTargetPath, deptJson);

                try
                {
                    var cacheDir = Path.GetDirectoryName(Globals.g_DepartmentCachePath);
                    if (!string.IsNullOrEmpty(cacheDir)) Directory.CreateDirectory(cacheDir);
                    await File.WriteAllTextAsync(Globals.g_DepartmentCachePath, deptJson);
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Could not mirror departments.json to cache: {ex.Message}");
                }

                try
                {
                    await _deptService.ReloadDataAsync();
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Failed to reload department service: {ex.Message}");
                }

                Log.Info(Tag, $"Persisted {deptChanges.Count} department change(s) and {supportTeamChanges.Count} support team change(s).");
            }

            // 2. Persist Links changes to links.json
            if (linkChanges.Count > 0)
            {
                var linksData = await LoadLinksDataAsync();
                ApplyLinkChanges(linksData, stagedChanges);

                string linksJson = JsonConvert.SerializeObject(linksData, Formatting.Indented);
                await File.WriteAllTextAsync(LinksTargetPath, linksJson);

                try
                {
                    var cacheDir = Path.GetDirectoryName(Globals.g_LinksCachePath);
                    if (!string.IsNullOrEmpty(cacheDir)) Directory.CreateDirectory(cacheDir);
                    await File.WriteAllTextAsync(Globals.g_LinksCachePath, linksJson);
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Could not mirror links.json to cache: {ex.Message}");
                }

                try
                {
                    await _linksService.ReloadLinksDataAsync();
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Failed to reload links service: {ex.Message}");
                }

                Log.Info(Tag, $"Persisted {linkChanges.Count} link change(s).");
            }
        }

        #endregion
    }
}
