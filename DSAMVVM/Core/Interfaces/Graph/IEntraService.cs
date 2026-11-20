using DSAMVVM.MVVM.Model.Entra;

namespace DSAMVVM.Core.Interfaces.Graph
{
    public interface IEntraService
    {
        Task<EntraDeviceSearchResult> SearchDevicesAsync(string query, CancellationToken cancellationToken = default);
    }
}
