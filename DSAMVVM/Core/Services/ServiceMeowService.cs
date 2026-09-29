using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Data;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;

namespace DSAMVVM.Core.Services
{
    // Loads ServiceMeow data using a Remote-First strategy with local offline fallback and image caching.
    public class ServiceMeowService(IHttpService http, IImageCacheService imageCacheService) : IServiceMeowService
    {
        private const string Tag = "ServiceMeow";

        private readonly IHttpService _http = http ?? throw new ArgumentNullException(nameof(http));
        private readonly IImageCacheService _imageCacheService = imageCacheService ?? throw new ArgumentNullException(nameof(imageCacheService));
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _cachePath = Globals.g_ServiceMeowCachePath;

        private ServiceMeowData? _cache;

        public ServiceMeowData? GetCachedData() => _cache;

        public Task<ServiceMeowData?> LoadServiceMeowDataAsync(CancellationToken ct = default)
            => LoadInternalAsync(isReload: false, ct);

        public Task ReloadServiceMeowDataAsync(CancellationToken ct = default)
            => LoadInternalAsync(isReload: true, ct);

        public Task<ImageSource?> GetPetImageAsync(ServiceMeowPet? pet, bool forceRefresh = false, CancellationToken ct = default)
        {
            if (pet == null || string.IsNullOrWhiteSpace(pet.ImageUrl))
            {
                return Task.FromResult<ImageSource?>(null);
            }

            return _imageCacheService.GetImageAsync(pet.ImageUrl, forceRefresh: forceRefresh, decodePixelWidth: 500, ct: ct);
        }

        public Task<ImageSource?> GetImageAsync(string? url, bool forceRefresh = false, int decodePixelWidth = 500, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Task.FromResult<ImageSource?>(null);
            }

            return _imageCacheService.GetImageAsync(url, forceRefresh: forceRefresh, decodePixelWidth: decodePixelWidth, ct: ct);
        }

        public ServiceMeowPet? GetRandomPet()
        {
            var pets = _cache?.AllPets;
            if (pets == null || pets.Count == 0) return null;
            return pets[Random.Shared.Next(pets.Count)];
        }

        private async Task<ServiceMeowData?> LoadInternalAsync(bool isReload, CancellationToken ct)
        {
            if (!isReload && _cache != null) return _cache;

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!isReload && _cache != null) return _cache;

                var key = isReload ? "ServiceMeow.Reload" : "ServiceMeow.Load";
                var progressKey = UiNotify.ProgressOf(key);
                UiNotify.Progress(key, isReload ? "Refreshing ServiceMeow..." : "Loading ServiceMeow...", priority: 0);

                var settings = (System.Windows.Application.Current != null) ? App.Settings?.Paths?.ServiceMeowData : null;
                string source = "Web";
                string targetUri = Globals.g_ServiceMeowJSON;

                if (settings is { UseCustomSource: true } && !string.IsNullOrWhiteSpace(settings.Uri))
                {
                    source = settings.Source;
                    targetUri = settings.Uri;
                }

                var sw = Stopwatch.StartNew();
                string jsonContent = string.Empty;
                bool loadedFromCache = false;
                bool jsonChanged = false;
                ServiceMeowData? previousModel = _cache;

                try
                {
                    if (string.Equals(source, "Web", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Info(Tag, $"Fetching ServiceMeow remote data from: {targetUri}");

                        var webContent = await _http.GetStringAsync(targetUri, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

                        // Read existing cache to verify write-on-change
                        string cachedContent = string.Empty;
                        if (File.Exists(_cachePath))
                        {
                            cachedContent = await File.ReadAllTextAsync(_cachePath, ct).ConfigureAwait(false);
                            if (previousModel == null)
                            {
                                try
                                {
                                    previousModel = JsonConvert.DeserializeObject<ServiceMeowData>(cachedContent);
                                }
                                catch { /* best-effort parse */ }
                            }
                        }

                        // Write-on-Change: Only overwrite disk cache if content differs
                        if (!string.Equals(webContent, cachedContent, StringComparison.Ordinal))
                        {
                            jsonChanged = true;
                            EnsureDirectory(_cachePath);
                            await File.WriteAllTextAsync(_cachePath, webContent, ct).ConfigureAwait(false);
                            Log.Info(Tag, "Remote ServiceMeow data changed. Disk backup cache updated.");
                        }
                        else
                        {
                            Log.Info(Tag, "Remote ServiceMeow data identical to disk cache. Skipping disk write.");
                        }

                        jsonContent = webContent;
                    }
                    else if (string.Equals(source, "File", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Info(Tag, $"Loading ServiceMeow local file: {targetUri}");

                        if (File.Exists(targetUri))
                        {
                            jsonContent = await File.ReadAllTextAsync(targetUri, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Custom ServiceMeow file not found: {targetUri}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    UiNotify.RemoveKey(progressKey);
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Primary ServiceMeow load failed ({source}): {ex.Message}");

                    // Fallback 1: Disk backup cache
                    if (string.Equals(source, "Web", StringComparison.OrdinalIgnoreCase) && File.Exists(_cachePath))
                    {
                        Log.Info(Tag, "Falling back to local ServiceMeow disk cache backup.");
                        try
                        {
                            jsonContent = await File.ReadAllTextAsync(_cachePath, ct).ConfigureAwait(false);
                            loadedFromCache = true;
                        }
                        catch (Exception cacheEx)
                        {
                            Log.Error(Tag, $"ServiceMeow cache read failed: {cacheEx.Message}");
                        }
                    }

                    // Fallback 2: Check application base directory
                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        var localBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "servicemeow.json");
                        if (File.Exists(localBase))
                        {
                            try
                            {
                                jsonContent = await File.ReadAllTextAsync(localBase, ct).ConfigureAwait(false);
                                loadedFromCache = true;
                                Log.Info(Tag, "Falling back to bundled servicemeow.json.");
                            }
                            catch (Exception baseEx)
                            {
                                Log.Error(Tag, $"Bundled servicemeow.json read failed: {baseEx.Message}");
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        sw.Stop();
                        UiNotify.RemoveKey(progressKey);
                        Log.Error(Tag, $"Could not load ServiceMeow data from source or cache: {ex.Message}");
                        return _cache;
                    }
                }

                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        var model = JsonConvert.DeserializeObject<ServiceMeowData>(jsonContent);
                        if (model != null)
                        {
                            foreach (var owner in model.Owners)
                            {
                                foreach (var pet in owner.Pets)
                                {
                                    pet.Owner = owner;
                                }
                            }
                            model.Meta?.Normalize();
                            _cache = model;

                            // When JSON changes, reconcile image cache (re-download updated images, purge removed images)
                            if (jsonChanged && previousModel != null)
                            {
                                _ = SyncChangedImagesAsync(previousModel, model, ct);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Tag, $"Failed to parse ServiceMeow JSON: {ex.Message}", ex);
                    }
                }

                sw.Stop();
                UiNotify.RemoveKey(progressKey);

                if (_cache != null)
                {
                    string sourceDesc = loadedFromCache ? "Cache Backup" : source;
                    Log.Info(Tag, $"Loaded ServiceMeow from {sourceDesc} in {sw.ElapsedMilliseconds}ms ({_cache.AllPets.Count} pets).");
                }

                return _cache;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task SyncChangedImagesAsync(ServiceMeowData oldData, ServiceMeowData newData, CancellationToken ct)
        {
            try
            {
                var oldUrls = oldData.AllPets
                    .Select(p => p.ImageUrl)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Cast<string>()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newUrls = newData.AllPets
                    .Select(p => p.ImageUrl)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Cast<string>()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Purge removed images
                foreach (var removedUrl in oldUrls.Except(newUrls))
                {
                    var path = _imageCacheService.GetCachedFilePath(removedUrl);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                            Log.Info(Tag, $"Purged removed pet image from cache: {path}");
                        }
                        catch { /* best-effort delete */ }
                    }
                }

                // Pre-cache newly added or updated images
                foreach (var addedUrl in newUrls.Except(oldUrls))
                {
                    Log.Info(Tag, $"Pre-caching new pet image: {addedUrl}");
                    _ = _imageCacheService.GetImageAsync(addedUrl, forceRefresh: true, ct: ct);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"SyncChangedImagesAsync encountered non-critical error: {ex.Message}");
            }
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}

