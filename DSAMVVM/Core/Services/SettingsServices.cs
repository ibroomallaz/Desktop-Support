using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;

namespace DSAMVVM.Core.Services
{
    public class SettingsService : ISettingsService
    {
        // one lock per final file path (case-insensitive)
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _saveLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private static SemaphoreSlim SaveLockFor(string path) =>
            _saveLocks.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));

        public async Task<AppSettings> LoadAsync(string settingsPath, CancellationToken ct = default)
        {
            var path = Expand(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var settings = new AppSettings(); // start with defaults

            if (File.Exists(path))
            {
                string? json = null;

                try
                {
                    json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                    var fileSchema = ReadSchemaVersion(json);

                    if (fileSchema > Globals.g_SettingsSchema)
                    {
                        var legacyDir = Path.Combine(Path.GetDirectoryName(path)!, "settings-legacy");
                        Directory.CreateDirectory(legacyDir);
                        var legacyFile = Path.Combine(
                            legacyDir,
                            $"settings.schema{fileSchema}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.json"
                        );
                        File.Move(path, legacyFile, overwrite: false);

                        SeedDefaults(settings);
                        EnsureDirectories(settings);
                        settings.ApplyDefaultsAndClamp();
                        await SaveAsync(settings, settingsPath, ct).ConfigureAwait(false);
                        return settings;
                    }

                    var loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (loaded is not null)
                    {
                        SeedViewFontSizes(loaded);
                        EnsureDirectories(loaded);
                        loaded.ApplyDefaultsAndClamp();
                        return loaded;
                    }
                }
                catch
                {
                    // fall through to try backup
                }

                // Try .bak if present
                var bak = path + ".bak";
                if (File.Exists(bak))
                {
                    try
                    {
                        var bakJson = await File.ReadAllTextAsync(bak, ct).ConfigureAwait(false);
                        var fromBak = JsonConvert.DeserializeObject<AppSettings>(bakJson);
                        if (fromBak is not null)
                        {
                            SeedViewFontSizes(fromBak);
                            EnsureDirectories(fromBak);
                            fromBak.ApplyDefaultsAndClamp();

                            // Restore .bak back to main atomically
                            await WriteTextAtomicallyAsync(path, bakJson, ct).ConfigureAwait(false);
                            return fromBak;
                        }
                    }
                    catch
                    {
                        // ignore and fall back to defaults
                    }
                }
            }

            // No file or both current/bak unusable -> defaults
            SeedDefaults(settings);
            EnsureDirectories(settings);
            settings.ApplyDefaultsAndClamp();
            await SaveAsync(settings, settingsPath, ct).ConfigureAwait(false);
            return settings;
        }

        public async Task SaveAsync(AppSettings settings, string settingsPath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var path = Expand(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            try
            {
                settings.Meta.LastUpdatedUtc = DateTime.UtcNow;
                settings.Meta.SchemaVersion = Globals.g_SettingsSchema;

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);

                // validate before touching disk
                if (!IsValidJson(json))
                {
                    await WriteDiagnosticDumpAsync(path, json, "invalid-json", ct).ConfigureAwait(false);
                    return;
                }

                // per-path in-process lock
                var slim = SaveLockFor(path);
                await slim.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await WriteTextAtomicallyAsync(path, json, ct).ConfigureAwait(false);
                }
                finally
                {
                    slim.Release();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // record for analysis and discard changes
                await WriteDiagnosticDumpAsync(path, ex.ToString(), "save-exception", CancellationToken.None).ConfigureAwait(false);
            }
        }

        public string ResolveDataDir(AppSettings s)
        {
            Directory.CreateDirectory(Globals.g_AppDir);
            var dataDir = Path.Combine(Globals.g_AppDir, s.Paths.DataDir);
            Directory.CreateDirectory(dataDir);
            return dataDir;
        }

        public double GetFontSizeFor(string viewName, AppSettings s, double min = 8, double max = 24)
        {
            if (s.Ui.Font.ViewFontSizeOverride &&
                s.Ui.ViewFontSizes.TryGetValue(viewName, out var v) &&
                v is not null &&
                v.FontSize > 0)
            {
                return Math.Clamp(v.FontSize, min, max);
            }

            return Math.Clamp(s.Ui.Font.DefaultSize, min, max);
        }

        public async Task<string?> GetDataAsync(DataLocation loc, AppSettings s, HttpClient http, CancellationToken ct = default)
        {
            if (IsWeb(loc))
            {
                try
                {
                    var json = await http.GetStringAsync(loc.Uri, ct).ConfigureAwait(false);

                    TryValidateJson(json);

                    await WriteFallbackAsync(json, loc, s, ct).ConfigureAwait(false);
                    return json;
                }
                catch
                {
                    var fb = ResolveFallback(loc, s);
                    if (fb is not null && File.Exists(fb))
                        return await File.ReadAllTextAsync(fb, ct).ConfigureAwait(false);

                    return null;
                }
            }
            else
            {
                var primary = ResolvePrimaryFile(loc, s);
                if (primary is not null && File.Exists(primary))
                    return await File.ReadAllTextAsync(primary, ct).ConfigureAwait(false);

                var fb = ResolveFallback(loc, s);
                if (fb is not null && File.Exists(fb))
                    return await File.ReadAllTextAsync(fb, ct).ConfigureAwait(false);

                return null;
            }
        }

        // Helpers

        private static bool IsWeb(DataLocation loc) =>
            loc.Source.Equals("web", StringComparison.OrdinalIgnoreCase);

        private static string Expand(string path)
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            return Path.GetFullPath(expanded);
        }

        private static string Resolve(string baseDir, string relativeOrAbsolute)
        {
            var p = Expand(relativeOrAbsolute);
            return Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(baseDir, p));
        }

        private static void EnsureDirectories(AppSettings s)
        {
            Directory.CreateDirectory(Globals.g_AppDir);
            var dataDir = Path.Combine(Globals.g_AppDir, s.Paths.DataDir);
            Directory.CreateDirectory(dataDir);
        }

        private void SeedDefaults(AppSettings s)
        {
            SeedViewFontSizes(s);

            s.Paths.DepartmentData.Uri = Globals.g_DepartmentJSONURL;
            s.Paths.DepartmentData.FallbackFile = "departments.cache.json";
            s.Paths.DepartmentData.Source = InferSource(Globals.g_DepartmentJSONURL);

            s.Paths.LinksData.Uri = Globals.g_LinksJSON;
            s.Paths.LinksData.FallbackFile = "links.cache.json";
            s.Paths.LinksData.Source = InferSource(Globals.g_LinksJSON);
        }

        private static string InferSource(string uriOrPath)
        {
            if (string.IsNullOrWhiteSpace(uriOrPath))
                return "web";

            return uriOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   uriOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                   ? "web"
                   : "file";
        }

        private static void SeedViewFontSizes(AppSettings s)
        {
            EnsureView(s, "UserView", 14.0);
            EnsureView(s, "GroupView", 14.0);
            EnsureView(s, "ComputerView", 14.0);
            EnsureView(s, "LinksView", 14.0);
        }

        private static void EnsureView(AppSettings s, string key, double sizeIfMissing)
        {
            if (!s.Ui.ViewFontSizes.TryGetValue(key, out var v) || v is null)
                s.Ui.ViewFontSizes[key] = new ViewFontSetting { FontSize = sizeIfMissing };
        }

        private string? ResolveFallback(DataLocation loc, AppSettings s)
        {
            if (string.IsNullOrWhiteSpace(loc.FallbackFile))
                return null;

            var dataDir = ResolveDataDir(s);
            return Resolve(dataDir, loc.FallbackFile);
        }

        private string? ResolvePrimaryFile(DataLocation loc, AppSettings s)
        {
            if (string.IsNullOrWhiteSpace(loc.Uri))
                return null;

            var dataDir = ResolveDataDir(s);
            return Resolve(dataDir, loc.Uri);
        }

        private async Task WriteFallbackAsync(string json, DataLocation loc, AppSettings s, CancellationToken ct)
        {
            var fb = ResolveFallback(loc, s);
            if (fb is null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(fb)!);

            TryValidateJson(json);

            await WriteTextAtomicallyAsync(fb, json, ct).ConfigureAwait(false);
        }

        private static int ReadSchemaVersion(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                var ver = root["Meta"]?["SchemaVersion"]?.Value<int?>();
                return ver ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private static void TryValidateJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try { JToken.Parse(text); }
            catch { /* remove if some endpoints are not JSON */ }
        }

        private static bool IsValidJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            try { JToken.Parse(text); return true; }
            catch { return false; }
        }

        // Atomic writer with unique temp + retries (no fixed .tmp collisions)
        private static async Task WriteTextAtomicallyAsync(string finalPath, string content, CancellationToken ct)
        {
            string dir = Path.GetDirectoryName(finalPath)!;
            Directory.CreateDirectory(dir);

            string name = Path.GetFileName(finalPath);
            string tempPath = Path.Combine(dir, $"{name}.{Guid.NewGuid():N}.tmp");
            string bakPath = finalPath + ".bak";

            // Write to unique temp with exclusive handle
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, useAsync: true))
            {
                await fs.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                await fs.FlushAsync(ct).ConfigureAwait(false);
                fs.Flush(true); // force to disk
            }

            await ReplaceWithRetriesAsync(tempPath, finalPath, bakPath, ct).ConfigureAwait(false);

            // best-effort cleanup (should not exist if Replace succeeded)
            TryDeleteQuiet(tempPath);
        }

        private static async Task ReplaceWithRetriesAsync(string temp, string final, string bak, CancellationToken ct)
        {
            const int maxAttempts = 6;
            int delayMs = 50;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (File.Exists(final))
                        File.Replace(temp, final, bak, ignoreMetadataErrors: true);
                    else
                        File.Move(temp, final);
                    return; // success
                }
                catch (IOException) when (attempt < maxAttempts) { /* try again */ }
                catch (UnauthorizedAccessException) when (attempt < maxAttempts) { /* try again */ }

                await Task.Delay(delayMs, ct).ConfigureAwait(false);
                delayMs *= 2;
            }

            // last attempt - let errors bubble if still failing
            if (File.Exists(final))
                File.Replace(temp, final, bak, ignoreMetadataErrors: true);
            else
                File.Move(temp, final);
        }

        private static void TryDeleteQuiet(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
        // Add near other helpers in SettingsService
        private static async Task WriteDiagnosticDumpAsync(string targetSettingsPath, string payload, string tag, CancellationToken ct)
        {
            try
            {
                var logsDir = Globals.g_LogsDir;
                Directory.CreateDirectory(logsDir);

                var fileName = $"settings-{tag}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.log";
                var path = Path.Combine(logsDir, fileName);

                var sb = new StringBuilder()
                    .AppendLine("=== Settings Diagnostic Dump ===")
                    .AppendLine($"UTC:     {DateTime.UtcNow:O}")
                    .AppendLine($"Target:  {targetSettingsPath}")
                    .AppendLine($"Process: {Environment.ProcessPath}")
                    .AppendLine($"User:    {Environment.UserName}")
                    .AppendLine("--------------------------------")
                    .AppendLine(payload ?? string.Empty);

                await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
            }
            catch
            {
                // best-effort only — never throw from diagnostics
            }
        }

    }
}
