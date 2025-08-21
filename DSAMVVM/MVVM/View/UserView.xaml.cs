using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;

namespace DSAMVVM.MVVM.View
{
    public partial class UserView : UserControl
    {
        // Per-view key for settings (used only when ViewFontSizeOverride is enabled)
        private const string ViewKey = "UserView";

        private ISettingsService? _settingsSvc;
        private IOutputTextSettingsProvider? _notifier;

        public UserView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            var sp = App.Services;
            _settingsSvc = sp.GetRequiredService<ISettingsService>();
            _notifier = sp.GetRequiredService<IOutputTextSettingsProvider>();

            // Listen for global/per-view size changes triggered anywhere
            _notifier.Changed += OnOutputFontSettingsChanged;

            // Apply once on load
            ApplyEffectiveFontSize();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_notifier != null)
                _notifier.Changed -= OnOutputFontSettingsChanged;
        }

        private void OnOutputFontSettingsChanged(object? sender, EventArgs e)
        {
            ApplyEffectiveFontSize();
        }

        private void ApplyEffectiveFontSize()
        {
            if (_settingsSvc == null) return;

            // Uses service's effective logic (global or per-view if override is on),
            // and clamps
            double size = _settingsSvc.GetFontSizeFor(ViewKey, App.Settings, min: 8, max: 24);

            var viewer = FindOutputViewer();
            if (viewer == null) return;

            viewer.Document ??= new FlowDocument();
            viewer.Document.FontSize = size;
        }

        private FlowDocumentScrollViewer? FindOutputViewer()
        {
            // Prefer a named element if present in XAML (e.g., x:Name="OutputViewer")
            if (this.FindName("OutputViewer") is FlowDocumentScrollViewer named) return named;

            // Fallback: search visual tree
            return FindDescendant<FlowDocumentScrollViewer>(this);
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T typed) return typed;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = FindDescendant<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        // --- bottom bar buttons ---

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserViewModel vm)
                vm.ClearLog();
        }

        private void IncreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFont(+1);
        private void DecreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFont(-1);
        private void ResetFont_Click(object sender, RoutedEventArgs e) => ResetFont();

        private void AdjustFont(int delta)
        {
            if (_settingsSvc == null || _notifier == null) return;

            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _ = _settingsSvc.AdjustOutputFontSize(s, preferPerView ? ViewKey : null, delta, preferPerView);

            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        private void ResetFont()
        {
            if (_settingsSvc == null || _notifier == null) return;

            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _settingsSvc.ResetOutputFontSize(s, preferPerView ? ViewKey : null, preferPerView, defaultSize: 14);

            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }
    }
}
