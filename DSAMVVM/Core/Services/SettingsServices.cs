using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Model.Config.UI;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace DSAMVVM.Core.Services
{
    public class SettingsService : ISettingsService
    {
        private const string Tag = "SettingsService";
        private const int MinFont = 8;
        private const int MaxFont = 24;

        //Reactive Event Broker
        public event EventHandler<AppSettings>? SettingsChanged;

        // Load / Save (core)

        public async Task<AppSettings> LoadAsync(string settingsPath, CancellationToken ct = default)
        {
            var path = Expand(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            Log.Debug(Tag, $"LoadAsync: attempting to load settings from \"{path}\".");

            AppSettings settings;

            if (File.Exists(path))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                    settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                    Log.Info(Tag, $"LoadAsync: loaded settings ({json?.Length ?? 0} bytes).");
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"LoadAsync: failed to read settings, using defaults. Reason: {ex.Message}");
                    settings = new AppSettings();
                }
            }
            else
            {
                Log.Info(Tag, "LoadAsync: settings file not found. Creating with defaults.");
                settings = new AppSettings();
                try
                {
                    await SaveAsync(settings, path, ct).ConfigureAwait(false);
                    Log.Info(Tag, "LoadAsync: default settings written.");
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"LoadAsync: failed to write default settings. Reason: {ex.Message}");
                }
            }

            return settings;
        }

        public async Task SaveAsync(AppSettings settings, string settingsPath, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var path = Expand(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            TryTouchMeta(settings);

            var tmp = path + ".tmp";
            var bak = path + ".bak";
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);

            Log.Debug(Tag, $"SaveAsync: writing settings to temp \"{tmp}\" ({json.Length} bytes).");

            try
            {
                await File.WriteAllTextAsync(tmp, json, Encoding.UTF8, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"SaveAsync: failed writing temp file \"{tmp}\".", ex);
                throw;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Copy(path, bak, overwrite: true);
                    Log.Debug(Tag, $"SaveAsync: backup created at \"{bak}\".");
                }

                File.Copy(tmp, path, overwrite: true);
                File.Delete(tmp);
                Log.Info(Tag, $"SaveAsync: settings persisted to \"{path}\".");


                //Broadcast change to listening ViewModels
                SettingsChanged?.Invoke(this, settings);
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"SaveAsync: replace/cleanup failed for \"{path}\".", ex);
                throw;
            }
        }

        // Paths

        public string ResolveDataDir(AppSettings s)
        {
            Directory.CreateDirectory(Globals.g_AppDir);
            var dataDir = Path.Combine(Globals.g_AppDir, s.Paths.DataDir);
            Directory.CreateDirectory(dataDir);
            Log.Debug(Tag, $"ResolveDataDir: ensured \"{dataDir}\".");
            return dataDir;
        }

        public string ResolveLogDir(AppSettings s)
        {
            Directory.CreateDirectory(Globals.g_LogsDir);
            return Globals.g_LogsDir;
        }

        // Font sizing

        public double GetFontSizeFor(string viewName, AppSettings s, double min = MinFont, double max = MaxFont)
        {
            double result;
            if (s.Ui.Font.ViewFontSizeOverride &&
                s.Ui.ViewFontSizes.TryGetValue(viewName, out var v) &&
                v is not null &&
                v.FontSize > 0)
            {
                result = Math.Clamp(v.FontSize, min, max);
                Log.Debug(Tag, $"GetFontSizeFor[{viewName}]: using per-view={v.FontSize} -> {result}.");
            }
            else
            {
                var def = s.Ui.Font.DefaultSize > 0 ? s.Ui.Font.DefaultSize : 14;
                result = Math.Clamp(def, min, max);
                Log.Debug(Tag, $"GetFontSizeFor[{viewName}]: using default={def} -> {result}.");
            }

            return result;
        }

        public double AdjustOutputFontSize(AppSettings s, string? viewName, int delta, bool preferPerView)
        {
            ArgumentNullException.ThrowIfNull(s);

            if (preferPerView && !string.IsNullOrWhiteSpace(viewName))
            {
                var current = GetFontSizeFor(viewName, s, MinFont, MaxFont);
                var next = (int)Math.Clamp(current + delta, MinFont, MaxFont);

                if (!s.Ui.ViewFontSizes.TryGetValue(viewName, out var entry) || entry is null)
                {
                    entry = new ViewFontSetting();
                    s.Ui.ViewFontSizes[viewName] = entry;
                }
                entry.FontSize = next;

                Log.Info(Tag, $"AdjustOutputFontSize: per-view \"{viewName}\" {current} -> {next} (delta {delta}).");
                return next;
            }
            else
            {
                var current = s.Ui.Font.DefaultSize > 0 ? s.Ui.Font.DefaultSize : 14;
                var next = (int)Math.Clamp(current + delta, MinFont, MaxFont);
                s.Ui.Font.DefaultSize = next;

                Log.Info(Tag, $"AdjustOutputFontSize: global default {current} -> {next} (delta {delta}).");
                return next;
            }
        }

        public void ResetOutputFontSize(AppSettings s, string? viewName, bool preferPerView, int defaultSize = 14)
        {
            ArgumentNullException.ThrowIfNull(s);

            if (preferPerView && !string.IsNullOrWhiteSpace(viewName))
            {
                if (s.Ui.ViewFontSizes.Remove(viewName))
                {
                    Log.Info(Tag, $"ResetOutputFontSize: removed per-view override for \"{viewName}\".");
                }
                else
                {
                    Log.Debug(Tag, $"ResetOutputFontSize: no per-view override existed for \"{viewName}\".");
                }
            }
            else
            {
                var clamped = (int)Math.Clamp(defaultSize, MinFont, MaxFont);
                var prev = s.Ui.Font.DefaultSize;
                s.Ui.Font.DefaultSize = clamped;
                Log.Info(Tag, $"ResetOutputFontSize: global default {prev} -> {clamped}.");
            }
        }

        // Debounced save

        private readonly Lock _saveGate = new();
        private Timer? _saveTimer;
        private AppSettings? _pendingSettings;
        private string? _pendingPath;
        private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);

        public void RequestSave(AppSettings s, string settingsPath)
        {
            if (s is null || string.IsNullOrWhiteSpace(settingsPath)) return;

            // Broadcast change immediately so UI updates instantly
            SettingsChanged?.Invoke(this, s);

            lock (_saveGate)
            {
                _pendingSettings = s;
                _pendingPath = Expand(settingsPath);

                _saveTimer?.Dispose();
                _saveTimer = new Timer(async _ =>
                {
                    AppSettings? toSave;
                    string? path;

                    lock (_saveGate)
                    {
                        toSave = _pendingSettings;
                        path = _pendingPath;
                        _pendingSettings = null;
                        _pendingPath = null;
                        _saveTimer?.Dispose();
                        _saveTimer = null;
                    }

                    if (toSave != null && !string.IsNullOrWhiteSpace(path))
                    {
                        Log.Debug(Tag, $"RequestSave[TIMER]: persisting queued settings to \"{path}\".");
                        try
                        {
                            await SaveAsync(toSave, path).ConfigureAwait(false);
                            Log.Info(Tag, "RequestSave[TIMER]: settings persisted.");
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(Tag, $"RequestSave[TIMER]: SaveAsync failed. Will rely on later flush. Reason: {ex.Message}");
                        }
                    }
                }, null, SaveDebounce, Timeout.InfiniteTimeSpan);

                Log.Debug(Tag, $"RequestSave: scheduled debounce write for \"{_pendingPath}\" in {SaveDebounce.TotalMilliseconds} ms.");
            }
        }

        public void FlushPendingSaves()
        {
            lock (_saveGate)
            {
                _saveTimer?.Dispose();
                _saveTimer = null;

                if (_pendingSettings != null && !string.IsNullOrWhiteSpace(_pendingPath))
                {
                    try
                    {
                        Log.Debug(Tag, $"FlushPendingSaves: flushing queued save to \"{_pendingPath}\".");
                        SaveAsync(_pendingSettings, _pendingPath).GetAwaiter().GetResult();
                        Log.Info(Tag, "FlushPendingSaves: flushed successfully.");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"FlushPendingSaves: flush failed. Reason: {ex.Message}");
                    }
                    finally
                    {
                        _pendingSettings = null;
                        _pendingPath = null;
                    }
                }
                else
                {
                    Log.Debug(Tag, "FlushPendingSaves: nothing pending.");
                }
            }
        }

        // Helpers

        private static string Expand(string path) =>
            Environment.ExpandEnvironmentVariables(path ?? string.Empty);

        private static void TryTouchMeta(AppSettings s)
        {
            try
            {
                var meta = s.Meta;
                var prop = meta?.GetType()?.GetProperty("LastUpdatedUtc");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(meta, DateTime.UtcNow);
                }
            }
            catch
            {
                // best effort only
            }
        }
    }
}