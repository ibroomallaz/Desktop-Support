using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.Model
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
                        info.GroupMembers = new List<string> { "Group does not exist." };
                        info.MemberCount = 0;
                    }
                }
                catch (PrincipalServerDownException)
                {
                    info.Exists = false;
                    info.ErrorMessage = "Unable to connect to the domain controller.";
                    info.GroupMembers = new List<string> { info.ErrorMessage };
                    info.MemberCount = 0;
                }
                catch (Exception ex)
                {
                    info.Exists = false;
                    info.ErrorMessage = $"Error retrieving group: {ex.Message}";
                    info.GroupMembers = new List<string> { info.ErrorMessage };
                    info.MemberCount = 0;
                }

                return info;
            });
        }
    }
}
