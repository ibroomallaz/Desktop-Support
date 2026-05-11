using System.Net.Http;
using DSAMVVM.Core.Interfaces;
using Newtonsoft.Json;

namespace DSAMVVM.Core.IO
{
    public static class RemoteWithBackUpLoader
    {
        public static async Task<T?> LoadAsync<T>(
            IHttpService http,
            string remoteUrl,
            JsonFileCache<T> fileCache,
            Func<T, DateTime?> stampSelector,
            JsonSerializerSettings? jsonSettings = null,
            Action<T>? normalize = null,
            Action<string>? log = null,
            CancellationToken ct = default)
        {
            log?.Invoke($"loader.start url=\"{remoteUrl}\"");

            string? json = null;
            try
            {
                json = await http.GetStringAsync(remoteUrl, ct);
                log?.Invoke($"web.ok bytes={(json?.Length ?? 0)}");
            }
            catch (HttpRequestException ex)
            {
                log?.Invoke($"web.error {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                log?.Invoke("web.canceled");
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
                T? webModel = default;
                try
                {
                    webModel = JsonConvert.DeserializeObject<T>(json, jsonSettings ?? DefaultJson);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"web.parse.error {ex.Message}");
                    webModel = default;
                }

                if (webModel != null)
                {
                    try { normalize?.Invoke(webModel); } catch { /* best-effort */ }

                    var localModel = await fileCache.ReadAsync();
                    var localMissing = localModel == null;
                    if (localMissing) log?.Invoke("cache.local.miss");
                    else log?.Invoke("cache.local.hit");

                    if (!localMissing)
                    {
                        try { normalize?.Invoke(localModel!); } catch { /* best-effort */ }
                    }

                    var webStamp = SafeUtc(stampSelector, webModel);
                    var localStamp = localMissing ? (DateTime?)null : SafeUtc(stampSelector, localModel!);

                    var shouldWrite =
                        localMissing ||
                        (webStamp.HasValue && (!localStamp.HasValue || webStamp > localStamp));

                    if (localMissing)
                    {
                        log?.Invoke("backup.decision write=true reason=first-run");
                    }
                    else
                    {
                        log?.Invoke(
                            $"backup.decision write={(shouldWrite ? "true" : "false")} " +
                            $"reason={(shouldWrite ? "newer-web" : "not-newer")} " +
                            $"webStamp={(webStamp.HasValue ? webStamp.Value.ToString("o") : "null")} " +
                            $"localStamp={(localStamp.HasValue ? localStamp.Value.ToString("o") : "null")}");
                    }

                    if (shouldWrite)
                    {
                        try
                        {
                            await fileCache.WriteAsync(webModel);
                            log?.Invoke($"backup.write.ok kind={(localMissing ? "create" : "overwrite")}");
                        }
                        catch (Exception ex)
                        {
                            log?.Invoke($"backup.write.error {ex.Message}");
                        }
                    }
                    else
                    {
                        log?.Invoke("backup.write.skipped");
                    }

                    log?.Invoke("loader.result source=web");
                    return webModel; // use web regardless of relative age
                }
            }

            var local = await fileCache.ReadAsync();
            log?.Invoke(local == null ? "loader.result source=none" : "loader.result source=local");
            return local;
        }

        private static DateTime? SafeUtc<T>(Func<T, DateTime?> f, T model)
        {
            try
            {
                var dt = f(model);
                if (!dt.HasValue) return null;
                return dt.Value.Kind == DateTimeKind.Utc ? dt : dt.Value.ToUniversalTime();
            }
            catch { return null; }
        }

        private static readonly JsonSerializerSettings DefaultJson = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };
    }
}
