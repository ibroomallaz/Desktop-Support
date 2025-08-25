using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model.Config;
using System.Collections.Concurrent;

namespace DSAMVVM.Core.Services
{
    public sealed class OutputTextSettingsProvider : IOutputTextSettingsProvider
    {
        private readonly ISettingsService _settingsSvc;
        private readonly Func<AppSettings> _settingsAccessor;

        // Cache: viewName -> resolved font size ("" = global)
        private readonly ConcurrentDictionary<string, double> _cache =
            new(StringComparer.Ordinal);

        public event EventHandler? Changed;

        public OutputTextSettingsProvider(ISettingsService settingsSvc, Func<AppSettings> settingsAccessor)
        {
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _settingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
        }

        public double GetFontSize(string? viewName = null)
        {
            var key = string.IsNullOrWhiteSpace(viewName) ? string.Empty : viewName;
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var s = _settingsAccessor();
            var size = _settingsSvc.GetFontSizeFor(key, s);
            _cache[key] = size;
            return size;
        }

        public void NotifyChanged()
        {
            // Invalidate cached sizes so next call re-reads from SettingsService
            _cache.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
