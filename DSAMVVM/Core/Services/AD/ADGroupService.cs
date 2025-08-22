using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System.DirectoryServices.AccountManagement;


namespace DSAMVVM.Core.Services.AD
{
    public class ADGroupService
    {
        private readonly string _domain;

        public ADGroupService(string domain)
        {
            _domain = domain;
        }

        public Task<ADGroupInfo> GetGroupAsync(string groupName)
        {
            return Task.Run(() =>
            {
                var info = new ADGroupInfo();

                try
                {
                    using var context = new PrincipalContext(ContextType.Domain, _domain);
                    using var group = GroupPrincipal.FindByIdentity(context, IdentityType.Name, groupName);

                    if (group != null)
                    {
                        var members = group.GetMembers()
                            .Select(p => p.Name)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .ToList();

                        info.Exists = true;
                        info.GroupMembers = members;
                        info.MemberCount = members.Count;

                        if (info.MemberCount == 0)
                        {
                            info.GroupMembers.Add("No group members exist.");
                        }
                    }
                    else
                    {
                        info.Exists = false;
                        info.GroupMembers = ["Group does not exist."];
                        info.MemberCount = 0;
                        UiNotify.Warn($"AD group '{groupName}' not found.");
                    }
                }
                catch (PrincipalServerDownException ex)
                {
                    info.Exists = false;
                    info.ErrorMessage = "Unable to connect to the domain controller.";
                    info.GroupMembers = [info.ErrorMessage];
                    info.MemberCount = 0;
                    // Surface -  actionable for the user (VPN/connection issues)
                    UiNotify.Error("AD group lookup failed", "Domain controller is unreachable.", ex, alsoStatusBar: true);
                }
                catch (Exception ex)
                {
                    info.Exists = false;
                    info.ErrorMessage = $"Error retrieving group: {ex.Message}";
                    info.GroupMembers = [info.ErrorMessage];
                    info.MemberCount = 0;

                    UiNotify.Error("AD group lookup failed", ex.Message, ex, alsoStatusBar: true);
                }

                return info;
            });
        }
    }
}
