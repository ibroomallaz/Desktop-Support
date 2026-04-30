using DSAMVVM.Core.Interfaces;

namespace DSAMVVM.Core.Renderers
{
    public static class OrganizationalRenderer
    {
        public static async Task<string> RenderDepartmentContextAsync(string deptNumber, IDepartmentService service)
        {
            var doc = new FlowDocMarkupBuilder();
            var dept = await service.GetDepartmentAsync(deptNumber);
            if (dept == null) return string.Empty;

            // 1. Support Team
            var teamName = await service.GetTeamAsync(dept.Number);
            if (!string.IsNullOrWhiteSpace(teamName))
            {
                doc.AddLink("Support Team: ", teamName.Trim(), $"dsa://team/{Uri.EscapeDataString(teamName.Trim())}");
            }

            // 2. Notes
            if (!string.IsNullOrWhiteSpace(dept.Notes)) doc.AddLabelValue("Notes: ", dept.Notes, false);

            // 3. File Repository (Kept in case a department has one)
            var repoPath = await service.GetFileRepoPathAsync(dept.Number);
            if (!string.IsNullOrWhiteSpace(repoPath))
                doc.AddLink("File Repository: ", "Open Repository", repoPath);

            doc.AddRaw(string.Empty);

            return doc.ToString();
        }

        public static async Task<string> RenderDivisionSupportAsync(string divCode, IDepartmentService service)
        {
            var doc = new FlowDocMarkupBuilder();
            doc.AddDim($"Looking up support for division '{divCode}'...");
            var teams = await service.GetTeamsByDivisionAsync(divCode);

            bool found = false;
            foreach (var team in teams)
            {
                found = true;
                doc.AddRaw(string.Empty);
                doc.AddTitle($"Support Team: {team.SupportTeamName}");
                if (!string.IsNullOrWhiteSpace(team.ManagerName))
                    doc.AddLabelValue("Manager: ", $"{team.ManagerName} ({team.ManagerNetID})");
                if (!string.IsNullOrWhiteSpace(team.PhoneNumber))
                    doc.AddLabelValue("Support Phone: ", team.PhoneNumber);
            }

            if (!found) doc.AddError($"No division '{divCode}' found.");
            return doc.ToString();
        }
        // --- DEEP LINK RENDERING: TEAM INFO ---
        public static async Task<string> RenderTeamInfoAsync(string teamName, IDepartmentService service)
        {
            var doc = new FlowDocMarkupBuilder();
            doc.AddRaw(string.Empty);

            var team = await service.GetSupportTeamAsync(teamName);
            if (team == null)
            {
                doc.AddError("Team not found.");
                return doc.ToString();
            }

            doc.AddTitle($"Team: {team.SupportTeamName}");
            if (!string.IsNullOrWhiteSpace(team.ManagerName))
            {
                doc.AddRaw($"[red]Manager: {team.ManagerName} [/red][gray]([/gray][red]{team.ManagerNetID}[/red][gray])[/gray]");
            }
            if (!string.IsNullOrWhiteSpace(team.PhoneNumber)) doc.AddLabelValue("Phone: ", team.PhoneNumber);

            if (team.SupportedDivisions != null && team.SupportedDivisions.Count > 0)
            {
                doc.AddRaw("[cyan]Supported Divisions:[/cyan]");
                foreach (var div in team.SupportedDivisions)
                {
                    doc.AddRaw($"[gray]  • [/gray][red]{div.DivAbbrev}[/red] [gray]-[/gray] [red]{div.DivFullName}[/red]");
                }
            }
            doc.AddRaw(string.Empty);
            return doc.ToString();
        }
    }
}