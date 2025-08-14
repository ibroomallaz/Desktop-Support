using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.Core.Services
{
    public class ADService : IADService
    {
        private readonly ADUserService _userService = new(Globals.g_domainPath, Globals.g_domainPathLDAP);
        private readonly ADComputerService _computerService = new(Globals.g_domainPathLDAP);
        private readonly ADGroupService _groupService = new(Globals.g_domainPath);

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
