using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.Core.Services.AD
{
    public abstract class ADService : IADService
    {
        private readonly ADUserService _userService;
        private readonly ADComputerService _computerService;
        private readonly ADGroupService _groupService;

        public ADService(string? ldapPath = null)
        {
            var path = ldapPath ?? Globals.g_domainPathLDAP;
            _userService = new ADUserService(path);
            _computerService = new ADComputerService(path);
            _groupService = new ADGroupService(path);
        }

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
            => _groupService.GetGroupAsync(groupName);

        public Task<MimLookupResult> GetUserMimGroupsAsync(string netid)
            => _groupService.GetUserMimGroupsAsync(netid);
    }
}
