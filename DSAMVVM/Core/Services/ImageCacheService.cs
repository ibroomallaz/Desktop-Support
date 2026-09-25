using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DSAMVVM.Core.Services
{
    // Service for downloading, caching, and serving images from local disk without file locking.
    public class ImageCacheService(IHttpService http) : IImageCacheService
    {
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly string _cacheDirectory = Globals.g_ServiceMeowImageCacheDir;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

        public async Task<ImageSource?> GetImageAsync(
            string? url,
            bool forceRefresh = false,
            int decodePixelWidth = 500,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                Log.Warn("ImageCache", $"Invalid image URL: '{url}'");
                return null;
            }

            var localPath = GetCachedFilePath(url);
            if (string.IsNullOrEmpty(localPath)) return null;

            var sem = _fileLocks.GetOrAdd(localPath, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                // 1. If not forcing a refresh and file exists, load immediately from disk
                if (!forceRefresh && File.Exists(localPath))
                {
                    var cachedBitmap = LoadBitmapFromDisk(localPath, decodePixelWidth);
                    if (cachedBitmap != null)
                    {
                        return cachedBitmap;
                    }

                    // If file was unreadable or corrupted, delete it
                    try { File.Delete(localPath); } catch { /* best-effort */ }
                }

                // 2. Download remote image
                try
                {
                    Log.Info("ImageCache", $"Downloading image from '{url}' to '{localPath}' (forceRefresh={forceRefresh})");
                    EnsureDirectory(localPath);

                    await _http.DownloadFileAsync(url, localPath, TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);

                    var downloadedBitmap = LoadBitmapFromDisk(localPath, decodePixelWidth);
                    if (downloadedBitmap != null)
                    {
                        return downloadedBitmap;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warn("ImageCache", $"Failed to download image from '{url}': {ex.Message}");

                    // Fallback: If refresh failed but previous disk cache still exists, return it
                    if (File.Exists(localPath))
                    {
                        Log.Info("ImageCache", $"Falling back to existing disk cache for '{url}'");
                        return LoadBitmapFromDisk(localPath, decodePixelWidth);
                    }
                }

                return null;
            }
            finally
            {
                sem.Release();
            }
        }

        public string? GetCachedFilePath(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var hash = ComputeHash(url.Trim());
            var extension = GetImageExtension(url);

            return Path.Combine(_cacheDirectory, $"{hash}{extension}");
        }

        public bool IsCached(string? url)
        {
            var path = GetCachedFilePath(url);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public Task ClearCacheAsync(CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(_cacheDirectory))
                    {
                        foreach (var file in Directory.GetFiles(_cacheDirectory))
                        {
                            try { File.Delete(file); } catch { /* best-effort */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("ImageCache", $"Failed to clear image cache directory: {ex.Message}");
                }
            }, ct);
        }

        // Decodes the image from a byte stream into a frozen BitmapImage, completely avoiding file locks on disk.
        private static BitmapSource? LoadBitmapFromDisk(string filePath, int decodePixelWidth)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                var bytes = File.ReadAllBytes(filePath);
                if (bytes.Length == 0) return null;

                using var stream = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                if (decodePixelWidth > 0)
                {
                    bitmap.DecodePixelWidth = decodePixelWidth;
                }

                bitmap.StreamSource = stream;
                bitmap.EndInit();

                // Freezes the bitmap to make it thread-safe and safe to bind across threads
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Warn("ImageCache", $"Failed to decode image file '{filePath}': {ex.Message}");
                return null;
            }
        }

        private static string ComputeHash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static string GetImageExtension(string url)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico")
                    {
                        return ext;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return ".png";
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
