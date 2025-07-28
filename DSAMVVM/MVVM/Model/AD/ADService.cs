using DSAMVVM.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.Model
{
    public class ADService : IADService
    {
        private readonly ADUserService _userService;
        private readonly ADComputerService _computerService;
        private readonly ADGroupService _groupService;

        public ADService()
        {
            _userService = new ADUserService(Globals.g_domainPath, Globals.g_domainPathLDAP);
            _computerService = new ADComputerService(Globals.g_domainPathLDAP);
            _groupService = new ADGroupService(Globals.g_domainPath);
        }

        // User
        public Task<ADUserInfo> GetUserAsync(string netid)
            => _userService.GetUserAsync(netid);

        public Task<List<string>> GetMimGroupsAsync(string netid)
            => _userService.GetMimGroupsAsync(netid);

        public Task<string?> LookupNameByEmployeeID(string userNumber)
            => _userService.LookupNameByEmployeeID(userNumber);

        // Computer
        public Task<ADComputerInfo> GetComputerAsync(string hostname)
            => _computerService.GetComputerAsync(hostname);

        // Group
        public Task<ADGroupInfo> GetGroupAsync(string groupName)
            => _groupService.GetGroupAsync(groupName);
    }
}
