using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model.Config;

namespace DSAMVVM.Core.Interfaces
{
    public interface ISettingsService
    {
        // Load settings from path (or return defaults if not found / corrupt).
        Task<AppSettings> LoadAsync(string settingsPath, CancellationToken ct = default);

        // Save settings to path.
        Task SaveAsync(AppSettings settings, string settingsPath, CancellationToken ct = default);

        // Get (and ensure) the department data file path based on settings.
        string ResolveDeptPath(AppSettings s);

        // Get (and ensure) the app logs directory path based on settings.
        string ResolveLogDir(AppSettings s);

        // Effective font size for a given view, honoring per-view overrides and clamping.
        double GetFontSizeFor(string viewName, AppSettings s, double min = 9, double max = 18);

        // --- Font-size helpers (settings-first model) ---

        // Apply +delta to global default OR per-view (when preferPerView is true and viewName provided).
        // Returns the new effective size that was written.
        double AdjustOutputFontSize(AppSettings s, string? viewName, int delta, bool preferPerView);

        // Reset: if preferPerView==true and viewName supplied -> remove per-view override;
        // otherwise reset the global default to provided defaultSize.
        void ResetOutputFontSize(AppSettings s, string? viewName, bool preferPerView, int defaultSize = 12);

        // --- Debounced persistence ---

        // Queue a save; multiple calls within a short window coalesce into one write.
        void RequestSave(AppSettings s, string settingsPath);

        // Force any pending debounced save to flush now (e.g., on shutdown).
        void FlushPendingSaves();
        // Event raised when settings are changed (after normalization).
        event EventHandler<AppSettings> SettingsChanged;
    }
}
