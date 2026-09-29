using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Admin;
using DSAMVVM.MVVM.Model.Data;
using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;
using System.IO;

namespace DSAMVVM.Core.Services
{
    public class AdminService(IDepartmentService deptService, ILinksService linksService, IServiceMeowService? meowService = null) : IAdminService
    {
        private const string Tag = "AdminService";

        private readonly IDepartmentService _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
        private readonly ILinksService _linksService = linksService ?? throw new ArgumentNullException(nameof(linksService));
        private readonly IServiceMeowService? _meowService = meowService;

        // --- Paths ---

        private static string DepartmentTargetPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "departments.json");

        private static string LinksTargetPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "links.json");

        private static string ServiceMeowTargetPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "servicemeow.json");


        // --- Department Operations ---

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

            foreach (var st in wrapper.SupportTeams.Where(st => !string.IsNullOrWhiteSpace(st.SupportTeamName)))
            {
                teamSet.Add(st.SupportTeamName.Trim());
            }

            return [.. teamSet.OrderBy(t => t)];
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


        // --- Support Team Operations ---

        public async Task<IReadOnlyList<SupportTeam>> LoadSupportTeamsAsync()
        {
            var wrapper = await LoadDepartmentsAsync();
            return [.. wrapper.SupportTeams.OrderBy(t => t.SupportTeamName)];
        }

        public async Task<SupportTeam?> FindSupportTeamAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            var trimmed = query.Trim();
            var wrapper = await LoadDepartmentsAsync();
            var teams = wrapper.SupportTeams;

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


        // --- Links Operations ---

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
            foreach (var group in links.TeamLinks.Where(group => !string.IsNullOrWhiteSpace(group.Team)))
            {
                teamSet.Add(group.Team.Trim());
            }

            var deptTeams = await GetAvailableDepartmentTeamsAsync();
            foreach (var team in deptTeams.Where(team => !string.IsNullOrWhiteSpace(team)))
            {
                teamSet.Add(team.Trim());
            }

            return [.. teamSet.OrderBy(t => t)];
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


        // --- ServiceMeow Operations ---

        private static ServiceMeowData CreateDefaultServiceMeowData()
        {
            return new ServiceMeowData
            {
                Meta = new ServiceMeowMeta
                {
                    SchemaVersion = Globals.g_ServiceMeowJSONSchema,
                    LastUpdatedUtc = DateTime.UtcNow
                },
                Owners =
                [
                    new ServiceMeowOwner
                    {
                        NetId = "davet",
                        Name = "Dave T.",
                        Team = "Network Operations",
                        Pets =
                        [
                            new ServiceMeowPet
                            {
                                Name = "Nimbus",
                                Species = "Cat",
                                Breed = "British Shorthair",
                                Title = "Chief Packet Sniffer",
                                Blurb = "Discovered a loose patch cable by chewing on the boot."
                            },
                            new ServiceMeowPet
                            {
                                Name = "Barnaby",
                                Species = "Dog",
                                Breed = "Golden Retriever",
                                Title = "Lead Morale Specialist",
                                Blurb = "Has a 99.9% success rate resolving escalated user stress tickets."
                            }
                        ]
                    },
                    new ServiceMeowOwner
                    {
                        NetId = "sarahm",
                        Name = "Sarah M.",
                        Team = "Service Desk",
                        Pets =
                        [
                            new ServiceMeowPet
                            {
                                Name = "Pixel",
                                Species = "Cat",
                                Breed = "Calico",
                                Title = "Senior Cable Untangler",
                                Blurb = "Always sleeps directly on top of the warmest switch rack."
                            }
                        ]
                    }
                ]
            };
        }

        public async Task<ServiceMeowData> LoadServiceMeowDataAsync()
        {
            try
            {
                string targetPath = ServiceMeowTargetPath;
                if (!File.Exists(targetPath) && File.Exists(Globals.g_ServiceMeowCachePath))
                {
                    targetPath = Globals.g_ServiceMeowCachePath;
                }

                if (File.Exists(targetPath))
                {
                    string json = await File.ReadAllTextAsync(targetPath);
                    var data = JsonConvert.DeserializeObject<ServiceMeowData>(json);
                    if (data != null)
                    {
                        foreach (var owner in data.Owners)
                        {
                            foreach (var pet in owner.Pets)
                            {
                                pet.Owner = owner;
                            }
                        }
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"Failed to load servicemeow data: {ex.Message}");
            }

            var fallback = CreateDefaultServiceMeowData();
            foreach (var owner in fallback.Owners)
            {
                foreach (var pet in owner.Pets)
                {
                    pet.Owner = owner;
                }
            }
            return fallback;
        }

        public async Task<ServiceMeowPet?> FindPetAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            var trimmed = query.Trim();
            var data = await LoadServiceMeowDataAsync();
            var allPets = data.AllPets;

            // 1. Exact match on Pet Name
            var match = allPets.FirstOrDefault(p =>
                string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // 2. Exact match on Owner NetID
            match = allPets.FirstOrDefault(p =>
                string.Equals(p.Owner?.NetId, trimmed, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // 3. Exact match on Owner Name
            match = allPets.FirstOrDefault(p =>
                string.Equals(p.Owner?.Name, trimmed, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // 4. Partial match on Pet Name
            match = allPets.FirstOrDefault(p =>
                p.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // 5. Partial match on Owner Name or NetID
            match = allPets.FirstOrDefault(p =>
                (p.Owner?.Name != null && p.Owner.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) ||
                (p.Owner?.NetId != null && p.Owner.NetId.Contains(trimmed, StringComparison.OrdinalIgnoreCase)));

            return match;
        }


        // --- Staging Helpers ---

        public DepartmentListWrapper ApplyDepartmentChanges(DepartmentListWrapper wrapper, IEnumerable<StagedChange> stagedChanges)
        {
            ArgumentNullException.ThrowIfNull(wrapper);
            ArgumentNullException.ThrowIfNull(stagedChanges);

            var changesList = stagedChanges as IReadOnlyList<StagedChange> ?? [.. stagedChanges];
            var deptChanges = changesList.Where(c => c.Section == AdminSection.Department);
            var supportTeamChanges = changesList.Where(c => c.Section == AdminSection.SupportTeam);

            // 1. Apply Department changes
            foreach (var change in deptChanges)
            {
                if (change.StagedData is not Department stagedDept) continue;

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

            // 2. Apply Support Team changes
            foreach (var change in supportTeamChanges)
            {
                if (change.StagedData is not StagedSupportTeamData stagedTeam) continue;

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

            wrapper.Meta.LastUpdatedUtc = DateTime.UtcNow;

            return wrapper;
        }

        public LinksData ApplyLinkChanges(LinksData linksData, IEnumerable<StagedChange> stagedChanges)
        {
            ArgumentNullException.ThrowIfNull(linksData);
            ArgumentNullException.ThrowIfNull(stagedChanges);

            var changesList = stagedChanges as IReadOnlyList<StagedChange> ?? [.. stagedChanges];
            var linkChanges = changesList.Where(c => c.Section == AdminSection.Links);

            foreach (var change in linkChanges)
            {
                if (change.StagedData is not StagedLinkData staged) continue;

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

                    if (group == null) continue;

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

            linksData.Meta.SchemaVersion = Globals.g_LinkJSONSchema;
            linksData.Meta.LastUpdatedUtc = DateTime.UtcNow;

            return linksData;
        }

        public ServiceMeowData ApplyServiceMeowChanges(ServiceMeowData meowData, IEnumerable<StagedChange> stagedChanges)
        {
            ArgumentNullException.ThrowIfNull(meowData);
            ArgumentNullException.ThrowIfNull(stagedChanges);

            var changesList = stagedChanges as IReadOnlyList<StagedChange> ?? [.. stagedChanges];
            var petChanges = changesList.Where(c => c.Section == AdminSection.ServiceMeow);

            foreach (var change in petChanges)
            {
                if (change.StagedData is not StagedServiceMeowData staged) continue;

                if (staged.Action == StagedServiceMeowAction.Delete)
                {
                    foreach (var owner in meowData.Owners)
                    {
                        var petToRemove = owner.Pets.FirstOrDefault(p =>
                            string.Equals(p.Id, staged.Pet.Id, StringComparison.OrdinalIgnoreCase) ||
                            (string.Equals(p.Name, staged.Pet.Name, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(owner.NetId, staged.OwnerNetId, StringComparison.OrdinalIgnoreCase)));

                        if (petToRemove == null) continue;
                        owner.Pets.Remove(petToRemove);
                        break;
                    }

                    meowData.Owners.RemoveAll(o => o.Pets.Count == 0);
                    continue;
                }

                // If reassigned from a different owner, remove from old owner
                if (!string.IsNullOrWhiteSpace(staged.OriginalOwnerNetId) &&
                    !string.Equals(staged.OriginalOwnerNetId, staged.OwnerNetId, StringComparison.OrdinalIgnoreCase))
                {
                    var oldOwner = meowData.Owners.FirstOrDefault(o =>
                        string.Equals(o.NetId, staged.OriginalOwnerNetId, StringComparison.OrdinalIgnoreCase));
                    if (oldOwner != null)
                    {
                        oldOwner.Pets.RemoveAll(p =>
                            string.Equals(p.Id, staged.Pet.Id, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Name, staged.Pet.Name, StringComparison.OrdinalIgnoreCase));
                        if (oldOwner.Pets.Count == 0)
                        {
                            meowData.Owners.Remove(oldOwner);
                        }
                    }
                }

                // Find target owner or create new
                var targetOwner = meowData.Owners.FirstOrDefault(o =>
                    string.Equals(o.NetId, staged.OwnerNetId, StringComparison.OrdinalIgnoreCase));

                if (targetOwner == null)
                {
                    targetOwner = new ServiceMeowOwner
                    {
                        NetId = staged.OwnerNetId.Trim(),
                        Name = staged.OwnerName.Trim(),
                        Team = staged.OwnerTeam.Trim(),
                        Pets = []
                    };
                    meowData.Owners.Add(targetOwner);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(staged.OwnerName))
                        targetOwner.Name = staged.OwnerName.Trim();
                    if (!string.IsNullOrWhiteSpace(staged.OwnerTeam))
                        targetOwner.Team = staged.OwnerTeam.Trim();
                }

                // Find or add pet under target owner
                var existingPet = targetOwner.Pets.FirstOrDefault(p =>
                    string.Equals(p.Id, staged.Pet.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Name, staged.Pet.Name, StringComparison.OrdinalIgnoreCase));

                if (existingPet != null)
                {
                    existingPet.Name = staged.Pet.Name;
                    existingPet.Species = staged.Pet.Species;
                    existingPet.Breed = staged.Pet.Breed;
                    existingPet.Title = staged.Pet.Title;
                    existingPet.Blurb = staged.Pet.Blurb;
                    existingPet.Images = [.. staged.Pet.Images];
                    existingPet.Owner = targetOwner;
                }
                else
                {
                    staged.Pet.Owner = targetOwner;
                    targetOwner.Pets.Add(staged.Pet);
                }
            }

            meowData.Meta.SchemaVersion = Globals.g_ServiceMeowJSONSchema;
            meowData.Meta.LastUpdatedUtc = DateTime.UtcNow;

            return meowData;
        }


        // --- Persistence ---

        public async Task SaveStagedChangesAsync(IEnumerable<StagedChange> stagedChanges)
        {
            ArgumentNullException.ThrowIfNull(stagedChanges);

            var changesList = stagedChanges as IReadOnlyList<StagedChange> ?? [.. stagedChanges];
            var deptChanges = changesList.Where(c => c.Section == AdminSection.Department).ToList();
            var supportTeamChanges = changesList.Where(c => c.Section == AdminSection.SupportTeam).ToList();
            var linkChanges = changesList.Where(c => c.Section == AdminSection.Links).ToList();
            var petChanges = changesList.Where(c => c.Section == AdminSection.ServiceMeow).ToList();

            if (deptChanges.Count == 0 && supportTeamChanges.Count == 0 && linkChanges.Count == 0 && petChanges.Count == 0) return;

            // 1. Persist Department & Support Team changes to departments.json
            if (deptChanges.Count > 0 || supportTeamChanges.Count > 0)
            {
                var wrapper = await LoadDepartmentsAsync();
                ApplyDepartmentChanges(wrapper, changesList);

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
                ApplyLinkChanges(linksData, changesList);

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

            // 3. Persist ServiceMeow changes to servicemeow.json
            if (petChanges.Count > 0)
            {
                var meowData = await LoadServiceMeowDataAsync();
                ApplyServiceMeowChanges(meowData, changesList);

                string meowJson = JsonConvert.SerializeObject(meowData, Formatting.Indented);
                await File.WriteAllTextAsync(ServiceMeowTargetPath, meowJson);

                try
                {
                    var cacheDir = Path.GetDirectoryName(Globals.g_ServiceMeowCachePath);
                    if (!string.IsNullOrEmpty(cacheDir)) Directory.CreateDirectory(cacheDir);
                    await File.WriteAllTextAsync(Globals.g_ServiceMeowCachePath, meowJson);
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Could not mirror servicemeow.json to cache: {ex.Message}");
                }

                Log.Info(Tag, $"Persisted {petChanges.Count} ServiceMeow change(s).");
            }
        }
    }
}

