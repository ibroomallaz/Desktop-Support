using DSAMVVM.MVVM.Model.Data;
using System.Windows.Media;

namespace DSAMVVM.Core.Interfaces.Integrations
{
    public interface IServiceMeowService
    {
        ServiceMeowData? GetCachedData();
        Task<ServiceMeowData?> LoadServiceMeowDataAsync(CancellationToken ct = default);
        Task ReloadServiceMeowDataAsync(CancellationToken ct = default);
        Task<ImageSource?> GetPetImageAsync(ServiceMeowPet? pet, bool forceRefresh = false, CancellationToken ct = default);
        Task<ImageSource?> GetImageAsync(string? url, bool forceRefresh = false, int decodePixelWidth = 500, CancellationToken ct = default);
        ServiceMeowPet? GetRandomPet();
    }
}
