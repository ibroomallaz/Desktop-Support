using DSAMVVM.MVVM.Model.AD;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DSAMVVM.Core
{
    public interface IADService
    {
        Task<ADUserInfo> GetUserAsync(string netid);
        Task<List<string>> GetMimGroupsAsync(string netid);
        //In case brought back for later:
        Task<string?> LookupNameByEmployeeID(string userNumber);

        Task<ADComputerInfo> GetComputerAsync(string hostname);

        Task<ADGroupInfo> GetGroupAsync(string groupName);
    }
}
