using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DSAMVVM.Core.Services.UI
{
    // Service for downloading, caching, and serving images from local disk and memory without file locking or race conditions.
    public class ImageCacheService(IHttpService http) : IImageCacheService
    {
        private const string Tag = "ImageCache";
        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly string _cacheDirectory = Globals.g_ServiceMeowImageCacheDir;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        private readonly ConcurrentDictionary<string, ImageSource> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

        public async Task<ImageSource?> GetImageAsync(
            string? url,
            bool forceRefresh = false,
            int decodePixelWidth = 500,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var memKey = $"{url.Trim()}_{decodePixelWidth}";

            switch (forceRefresh)
            {
                // 0. Fast-path: check in-memory cache first to avoid disk I/O and bitmap re-decoding
                case false when _memoryCache.TryGetValue(memKey, out var memoryImage):
                    return memoryImage;
                case true:
                    _memoryCache.TryRemove(memKey, out _);
                    break;
            }

            bool isWeb = false;
            bool isLocalFile = false;
            string? localSourcePath = null;

            if (File.Exists(url))
            {
                isLocalFile = true;
                localSourcePath = url;
            }
            else if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile && File.Exists(uri.LocalPath))
                {
                    isLocalFile = true;
                    localSourcePath = uri.LocalPath;
                }
                else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    isWeb = true;
                }
            }

            if (!isWeb && !isLocalFile)
            {
                Log.Warn(Tag, $"Invalid or unreachable image URL/path: '{url}'");
                return null;
            }

            var localPath = GetCachedFilePath(url);
            if (string.IsNullOrEmpty(localPath)) return null;

            var sem = _fileLocks.GetOrAdd(localPath, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                switch (forceRefresh)
                {
                    // Re-check memory cache inside lock in case another task loaded it concurrently
                    case false when _memoryCache.TryGetValue(memKey, out var concurrentMemImage):
                        return concurrentMemImage;
                    // 1. If not forcing a refresh and cached file exists, load immediately from disk cache
                    case false when File.Exists(localPath):
                    {
                        var cachedBitmap = LoadBitmapFromDisk(localPath, decodePixelWidth);
                        if (cachedBitmap != null)
                        {
                            _memoryCache[memKey] = cachedBitmap;
                            return cachedBitmap;
                        }

                        // If file was unreadable or corrupted, delete it
                        try { File.Delete(localPath); } catch { /* best-effort */ }

                        break;
                    }
                }

                // 2. Fetch and cache image (Local file copy or Web download)
                if (isLocalFile && !string.IsNullOrEmpty(localSourcePath))
                {
                    try
                    {
                        Log.Info(Tag, $"Copying local image from '{localSourcePath}' to cache '{localPath}' (forceRefresh={forceRefresh})");
                        EnsureDirectory(localPath);
                        File.Copy(localSourcePath, localPath, overwrite: true);

                        var localBitmap = LoadBitmapFromDisk(localPath, decodePixelWidth);
                        if (localBitmap != null)
                        {
                            _memoryCache[memKey] = localBitmap;
                            return localBitmap;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"Failed to copy local image from '{localSourcePath}': {ex.Message}");
                    }
                }
                else if (isWeb)
                {
                    try
                    {
                        Log.Info(Tag, $"Downloading image from '{url}' to '{localPath}' (forceRefresh={forceRefresh})");
                        EnsureDirectory(localPath);

                        await _http.DownloadFileAsync(url, localPath, TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);

                        var downloadedBitmap = LoadBitmapFromDisk(localPath, decodePixelWidth);
                        if (downloadedBitmap != null)
                        {
                            _memoryCache[memKey] = downloadedBitmap;
                            return downloadedBitmap;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"Failed to download image from '{url}': {ex.Message}");

                        // Fallback: If refresh failed but previous disk cache still exists, return it
                        if (File.Exists(localPath))
                        {
                            Log.Info(Tag, $"Falling back to existing disk cache for '{url}'");
                            var fallbackBitmap = LoadBitmapFromDisk(localPath, decodePixelWidth);
                            if (fallbackBitmap != null)
                            {
                                _memoryCache[memKey] = fallbackBitmap;
                                return fallbackBitmap;
                            }
                        }
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
            _memoryCache.Clear();

            return Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_cacheDirectory)) return;
                    foreach (var file in Directory.GetFiles(_cacheDirectory))
                    {
                        try { File.Delete(file); } catch { /* best-effort */ }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Failed to clear image cache directory: {ex.Message}");
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
                // In-memory stream decode with OnLoad avoids file locks; avoid IgnoreImageCache to prevent WPF .NET 10 null key bug in ImagingCache

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
                Log.Warn(Tag, $"Failed to decode image file '{filePath}': {ex.Message}");
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
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp")
                    {
                        return ext;
                    }
                }
                else
                {
                    var ext = Path.GetExtension(url).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp")
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
