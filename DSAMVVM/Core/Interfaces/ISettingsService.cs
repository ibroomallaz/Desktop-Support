using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DSAMVVM.MVVM.Model.Config;

namespace DSAMVVM.Core.Interfaces
{
    public interface ISettingsService
    {
        // Load settings from disk. If the file doesn't exist, seed defaults and create necessary directories.
        Task<AppSettings> LoadAsync(string settingsPath, CancellationToken ct = default);

        // Save settings to disk. Automatically updates lastUpdatedUtc.
        Task SaveAsync(AppSettings settings, string settingsPath, CancellationToken ct = default);

        // Get the data directory path (Globals.g_AppDir + settings.Paths.DataDir) and ensure it exists.
        string ResolveDataDir(AppSettings s);

        // Get the font size for the specified view, respecting the override flag and applying clamping.
        double GetFontSizeFor(string viewName, AppSettings s, double min = 9, double max = 24);

        // Load JSON data from a DataLocation.
        // For "web": fetch via HTTP, cache to fallbackFile, fallback to cached file on failure.
        // For "file": read primary file (relative to dataDir if not absolute), fallback to cached file if primary fails.
        // Returns null if nothing could be loaded.
        Task<string?> GetDataAsync(DataLocation loc, AppSettings s, HttpClient http, CancellationToken ct = default);
    }
}
