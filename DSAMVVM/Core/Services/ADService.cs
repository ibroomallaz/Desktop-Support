using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    public class ADService(IStatusReporter status) : IADService
    {
        private readonly ADUserService _userService = new(Globals.g_domainPath, Globals.g_domainPathLDAP, status);
        private readonly ADComputerService _computerService = new(Globals.g_domainPathLDAP, status);
        private readonly ADGroupService _groupService = new(Globals.g_domainPath, status);

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
