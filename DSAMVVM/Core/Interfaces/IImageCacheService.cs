using System.Windows.Media;

namespace DSAMVVM.Core.Interfaces
{
    public interface IImageCacheService
    {
        Task<ImageSource?> GetImageAsync(string? url, bool forceRefresh = false, int decodePixelWidth = 500, CancellationToken ct = default);
        string? GetCachedFilePath(string? url);
        bool IsCached(string? url);
        Task ClearCacheAsync(CancellationToken ct = default);
    }
}
