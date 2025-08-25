using DSAMVVM.Core; // Globals
using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.Core.Services.AD
{
    public class ADService : IADService
    {
        // user/computer services keep their current ctor args
        private readonly ADUserService _userService = new(Globals.g_domainPath, Globals.g_domainPathLDAP);
        private readonly ADComputerService _computerService = new(Globals.g_domainPathLDAP);

        // group service now uses the version that reads Globals internally (no ctor args)
        private readonly ADGroupService _groupService = new();

        // User
        public Task<ADUserInfo> GetUserAsync(string netid) => _userService.GetUserAsync(netid);
        public Task<string?> LookupNameByEmployeeID(string userNumber) => _userService.LookupNameByEmployeeID(userNumber);

        // Computer
        public Task<ADComputerInfo> GetComputerAsync(string hostname) => _computerService.GetComputerAsync(hostname);

        // Group
        public Task<ADGroupInfo> GetGroupAsync(string groupName) => _groupService.GetGroupAsync(groupName);
        public Task<MimLookupResult> GetUserMimGroupsAsync(string netid) => _groupService.GetUserMimGroupsAsync(netid);
    }
}
