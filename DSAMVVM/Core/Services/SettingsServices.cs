using System;
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
                    json = await File.ReadAllTextAsync(path, ct);
                    var fileSchema = ReadSchemaVersion(json);

                    if (fileSchema > Globals.g_SettingsSchema)
                    {
                        // Move future-schema file to "settings-legacy" and ignore
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
                        await SaveAsync(settings, settingsPath, ct);
                        return settings;
                    }

                    // Try to deserialize current file
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
                        var bakJson = await File.ReadAllTextAsync(bak, ct);
                        var fromBak = JsonConvert.DeserializeObject<AppSettings>(bakJson);
                        if (fromBak is not null)
                        {
                            SeedViewFontSizes(fromBak);
                            EnsureDirectories(fromBak);
                            fromBak.ApplyDefaultsAndClamp();
                            // Restore .bak back to main atomically
                            await WriteTextAtomicallyAsync(path, bakJson, ct);
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
            await SaveAsync(settings, settingsPath, ct);
            return settings;
        }

        public async Task SaveAsync(AppSettings settings, string settingsPath, CancellationToken ct = default)
        {
            // Guard: allow cancellation to behave normally
            ct.ThrowIfCancellationRequested();

            var path = Expand(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            try
            {
                // Update metadata just before serialize
                settings.Meta.LastUpdatedUtc = DateTime.UtcNow;
                settings.Meta.SchemaVersion = Globals.g_SettingsSchema;

                // 1) Serialize
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);

                // 2) Validate in-memory; if invalid, discard and keep last good
                if (!IsValidJson(json))
                {
                    await WriteDiagnosticDumpAsync(path, json, "invalid-json", ct);
                    return;
                }

                // 3) Atomic write (tmp + replace). If anything fails, we catch and discard changes.
                await WriteTextAtomicallyAsync(path, json, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Don’t crash UI; record for analysis and discard changes
                await WriteDiagnosticDumpAsync(path, ex.ToString(), "save-exception", CancellationToken.None);
                return;
            }
        }

        public string ResolveDataDir(AppSettings s)
        {
            Directory.CreateDirectory(Globals.g_AppDir);
            var dataDir = Path.Combine(Globals.g_AppDir, s.Paths.DataDir);
            Directory.CreateDirectory(dataDir);
            return dataDir;
        }

        public double GetFontSizeFor(string viewName, AppSettings s, double min = 9, double max = 24)
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
                    var json = await http.GetStringAsync(loc.Uri, ct);

                    // Optional: validate if you expect JSON
                    TryValidateJson(json);

                    await WriteFallbackAsync(json, loc, s, ct);
                    return json;
                }
                catch
                {
                    var fb = ResolveFallback(loc, s);
                    if (fb is not null && File.Exists(fb))
                        return await File.ReadAllTextAsync(fb, ct);

                    return null;
                }
            }
            else
            {
                var primary = ResolvePrimaryFile(loc, s);
                if (primary is not null && File.Exists(primary))
                    return await File.ReadAllTextAsync(primary, ct);

                var fb = ResolveFallback(loc, s);
                if (fb is not null && File.Exists(fb))
                    return await File.ReadAllTextAsync(fb, ct);

                return null;
            }
        }

        // ----------------- Helpers -----------------

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

            // Optional: validate JSON if you expect JSON
            TryValidateJson(json);

            await WriteTextAtomicallyAsync(fb, json, ct);
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

        private static async Task WriteTextAtomicallyAsync(string finalPath, string content, CancellationToken ct)
        {
            string dir = Path.GetDirectoryName(finalPath)!;
            Directory.CreateDirectory(dir);
            string tmp = finalPath + ".tmp";
            string bak = finalPath + ".bak";

            // Write to .tmp and flush to disk
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
            using (var sw = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                await sw.WriteAsync(content.AsMemory(), ct);
                await sw.FlushAsync();
                fs.Flush(true); // force to disk
            }

            // Atomic swap (NTFS). Creates/updates .bak for rollback
            try
            {
                File.Replace(tmp, finalPath, bak, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                // Cross-volume or FS without Replace support
                if (File.Exists(finalPath)) File.Copy(finalPath, bak, overwrite: true);
                File.Delete(finalPath);
                File.Move(tmp, finalPath);
            }
        }

        private static async Task WriteDiagnosticDumpAsync(string finalPath, string payload, string tag, CancellationToken ct)
        {
            try
            {
                var dir = Path.GetDirectoryName(finalPath)!;
                var logDir = Path.Combine(dir, "settings-logs");
                Directory.CreateDirectory(logDir);

                var name = $"{Path.GetFileNameWithoutExtension(finalPath)}.{tag}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
                var file = Path.Combine(logDir, name);

                await File.WriteAllTextAsync(file, payload ?? string.Empty, ct);
            }
            catch
            {
                // swallow: diagnostics should never cause further errors
            }
        }
    }
}
