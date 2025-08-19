using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model.Config;

namespace DSAMVVM.Core.Services
{
    public sealed class OutputTextSettingsProvider : IOutputTextSettingsProvider
    {
        private readonly ISettingsService _settingsSvc;
        private readonly Func<AppSettings> _settingsAccessor;

        public event EventHandler? Changed;

        public OutputTextSettingsProvider(ISettingsService settingsSvc, Func<AppSettings> settingsAccessor)
        {
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _settingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
        }

        public double GetFontSize(string? viewName = null)
        {
            var s = _settingsAccessor();
            var key = string.IsNullOrWhiteSpace(viewName) ? string.Empty : viewName;
            return _settingsSvc.GetFontSizeFor(key, s);
        }

        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
