using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.Core.Services.AD
{
    public class ADService : IADService
    {
        private readonly ADUserService _userService = new(Globals.g_domainPathLDAP);
        private readonly ADComputerService _computerService = new(Globals.g_domainPathLDAP);
        private readonly ADGroupService _groupService = new();

        // User

        public Task<ADUserInfo> GetUserAsync(string netid)
            => _userService.GetUserAsync(netid);

        public Task<string?> LookupNameByEmployeeID(string userNumber)
            => _userService.LookupNameByEmployeeID(userNumber);

        public Task<AdobeLicenseStatus> CheckAdobeLicensesAsync(string netid)
            => _userService.CheckAdobeLicensesAsync(netid);

        // Computer

        public Task<ADComputerInfo> GetComputerAsync(string hostname)
            => _computerService.GetComputerAsync(hostname);

        // Group

        public Task<ADGroupInfo> GetGroupAsync(string groupName)
            => ADGroupService.GetGroupAsync(groupName);

        public Task<MimLookupResult> GetUserMimGroupsAsync(string netid)
            => ADGroupService.GetUserMimGroupsAsync(netid);
    }
}