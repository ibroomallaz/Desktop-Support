using DSAMVVM.Core.Models;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.Core.Interfaces
{
    public interface IADService
    {
        Task<ADUserInfo> GetUserAsync(string netid);

        Task<string?> LookupNameByEmployeeID(string userNumber);

        Task<ADComputerInfo> GetComputerAsync(string hostname);

        Task<ADGroupInfo> GetGroupAsync(string groupName);

        Task<MimLookupResult> GetUserMimGroupsAsync(string netid);

        Task<AdobeLicenseStatus> CheckAdobeLicensesAsync(string netid);
    }
}