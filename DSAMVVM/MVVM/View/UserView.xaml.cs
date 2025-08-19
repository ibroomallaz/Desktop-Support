using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DSAMVVM.MVVM.View
{
    public partial class UserView : UserControl
    {
        // Per-view key for settings (used only when ViewFontSizeOverride is enabled)
        private const string ViewKey = "UserView";

        public UserView()
        {
            InitializeComponent();
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserViewModel vm)
                vm.ClearLog();
        }

        private void IncreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFont(+1);
        private void DecreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFont(-1);
        private void ResetFont_Click(object sender, RoutedEventArgs e) => ResetFont();

        // --- helpers ---

        private void AdjustFont(int delta)
        {
            var sp = App.Services;
            var settingsSvc = sp.GetRequiredService<ISettingsService>();
            var notifier = sp.GetRequiredService<IOutputTextSettingsProvider>();
            var s = App.Settings;

            // Global-first: only per-view when user has enabled that option in settings
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;
            settingsSvc.AdjustOutputFontSize(s, preferPerView ? ViewKey : null, delta, preferPerView);

            // instant UI refresh across all viewers
            notifier.NotifyChanged();

            // polite, debounced persistence
            var path = Path.Combine(Globals.g_AppDir, "settings.json");
            settingsSvc.RequestSave(s, path);
        }

        private void ResetFont()
        {
            var sp = App.Services;
            var settingsSvc = sp.GetRequiredService<ISettingsService>();
            var notifier = sp.GetRequiredService<IOutputTextSettingsProvider>();
            var s = App.Settings;

            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;
            settingsSvc.ResetOutputFontSize(s, preferPerView ? ViewKey : null, preferPerView, defaultSize: 14);

            notifier.NotifyChanged();

            var path = Path.Combine(Globals.g_AppDir, "settings.json");
            settingsSvc.RequestSave(s, path);
        }
    }
}
