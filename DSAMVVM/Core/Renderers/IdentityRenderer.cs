using System.Text;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.Core.Renderers
{
    public static class IdentityRenderer
    {
        public static string RenderADComputer(ADComputerInfo? comp)
        {
            var doc = new FlowDocMarkupBuilder();
            if (comp is null || !comp.Exists)
            {
                doc.AddError($"Search complete. Computer not found. Error: {comp?.ErrorMessage ?? "Unknown"}");
                return doc.ToString();
            }

            doc.AddRaw(string.Empty);
            doc.AddTitle(comp.Name);
            if (!string.IsNullOrWhiteSpace(comp.Description)) doc.AddLabelValue("Description: ", comp.Description);
            if (!string.IsNullOrWhiteSpace(comp.OperatingSystem)) doc.AddLabelValue("Operating System: ", comp.OperatingSystem);
            if (!string.IsNullOrWhiteSpace(comp.OUs)) doc.AddLabelValue("OUs: ", comp.OUs);
            if (!string.IsNullOrWhiteSpace(comp.LastLogonDate)) doc.AddLabelValue("Last Logon: ", comp.LastLogonDate);
            if (comp.Enabled == false) doc.AddLabelValue("Enabled: ", "False", false);
            doc.AddLabelValue("Hybrid Group Member: ", comp.IsHybridGroupMember ? "True" : "False", false);

            return doc.ToString();
        }

        public static string RenderADUser(ADUserInfo? user)
        {
            var doc = new FlowDocMarkupBuilder();
            if (user is null || !user.Exists)
            {
                doc.AddError($"Search complete. User not found. Error: {user?.ErrorMessage ?? "Unknown"}");
                return doc.ToString();
            }

            doc.AddRaw(string.Empty);
            doc.AddTitle(user.DisplayName);
            if (!string.IsNullOrEmpty(user.EduAffiliation)) doc.AddLabelValue("Affiliation: ", user.EduAffiliation);
            if (!string.IsNullOrEmpty(user.Division)) doc.AddLabelValue("Division: ", user.Division);
            if (!string.IsNullOrEmpty(user.DepartmentName)) doc.AddLabelValue("Department: ", user.DepartmentName);
            if (user.Enabled == false) doc.AddLabelValue("Enabled: ", "False", false);
            if (user.Locked == true) doc.AddLabelValue("Locked: ", "True", false);

            doc.AddRaw("[cyan]Software Licenses:[/cyan]");
            if (!string.IsNullOrWhiteSpace(user.RawLicense))
            {
                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.RawLicense));
                doc.AddListLink("O365: ", user.License ?? "Unknown", $"dsa://license/o365/{user.Name}/{b64}");
            }
            else doc.AddLabeledListItem("O365: ", user.License ?? "None");

            doc.AddListLink("Adobe: ", "Check", $"dsa://license/adobe/{user.Name}");
            return doc.ToString();
        }

        public static string RenderMimGroups(MimLookupResult r, string query)
        {
            var doc = new FlowDocMarkupBuilder();
            doc.AddRaw(string.Empty);
            doc.AddTitle($"MIM groups for user '{query}'");

            if (!r.Exists)
            {
                doc.AddError(string.IsNullOrWhiteSpace(r.Error) ? $"'{query}' is not a valid NetID." : r.Error);
                return doc.ToString();
            }

            if (r.Enabled == false) doc.AddLabelValue("Enabled: ", "False", false);
            doc.AddLabelValue("Total MIM groups: ", r.Groups?.Count.ToString() ?? "0", false);

            if (r.Groups is { Count: > 0 })
                foreach (var g in r.Groups) doc.AddListItem(g);
            else
                doc.AddRaw("[cyan]No valid MIM groups found.[/cyan]");

            return doc.ToString();
        }

        public static string RenderGroupMembers(ADGroupInfo info, string groupName)
        {
            var doc = new FlowDocMarkupBuilder();
            doc.AddRaw(string.Empty);
            doc.AddTitle($"Members of group '{groupName}'");

            if (info.Exists && info.MemberCount is int c)
            {
                doc.AddLabelValue("Total members: ", c.ToString(), false);
                if (c == 0) doc.AddRaw("[cyan]No group members exist.[/cyan]");
                else if (info.GroupMembers != null)
                    foreach (var m in info.GroupMembers) doc.AddListItem(m);
            }
            else doc.AddError(info.ErrorMessage ?? "Group not found or lookup failed.");

            return doc.ToString();
        }
        // --- DEEP LINK RENDERING: O365 RAW ---
        public static string RenderRawLicenseInfo(string netid, string rawLicense)
        {
            var doc = new FlowDocMarkupBuilder();
            doc.AddRaw(string.Empty);
            doc.AddTitle($"Raw AD License Attribute for {netid}:");
            doc.AddDim(rawLicense);
            doc.AddRaw(string.Empty);
            return doc.ToString();
        }

        // --- DEEP LINK RENDERING: ADOBE ---
        public static string RenderAdobeLicenseStatus(string netid, bool hasAcrobat, bool hasCC)
        {
            var doc = new FlowDocMarkupBuilder();
            doc.AddRaw(string.Empty);
            doc.AddTitle($"Adobe Licenses ({netid}):");

            string acroColor = hasAcrobat ? "green" : "red";
            string acroIcon = hasAcrobat ? "✓" : "✗";
            string acroText = hasAcrobat ? "Assigned" : "None";

            string ccColor = hasCC ? "green" : "red";
            string ccIcon = hasCC ? "✓" : "✗";
            string ccText = hasCC ? "Assigned" : "None";

            doc.AddRaw($"[cyan]  Acrobat Pro: [/cyan][{acroColor}]{acroIcon} {acroText}[/{acroColor}]");
            doc.AddRaw($"[cyan]  Creative Cloud: [/cyan][{ccColor}]{ccIcon} {ccText}[/{ccColor}]");
            doc.AddRaw(string.Empty);

            return doc.ToString();
        }
    }
}