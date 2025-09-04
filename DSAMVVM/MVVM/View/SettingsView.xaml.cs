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
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private double _sliderValue;

        public double SliderValue
        {
            get => _sliderValue;
            set
            {
                if (_sliderValue != value)
                {
                    _sliderValue = value;

                }
            }
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

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_notifier != null)
                _notifier.Changed -= OnOutputFontSettingsChanged;
        }

        // Per-view key for settings (used only when ViewFontSizeOverride is enabled)
        private const string ViewKey = "UserView";

        private ISettingsService? _settingsSvc;
        private IOutputTextSettingsProvider? _notifier;


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ResetFont();
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

        private void AdjustFont(int delta)
        {
            if (_settingsSvc == null || _notifier == null) return;

            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _ = _settingsSvc.AdjustOutputFontSize(s, preferPerView ? ViewKey : null, delta, preferPerView);

            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }


        private void GeneralFontChecked(object sender, RoutedEventArgs e)
        {
            FontSlider.Visibility = Visibility.Visible;
            FontSlider2.Visibility = Visibility.Visible;
            GeneralFont.Visibility = Visibility.Collapsed;
            checkBox.Content = "General View Font Settings";

        }

        private void GeneralFontUnChecked(object sender, RoutedEventArgs e)
        {
            FontSlider.Visibility = Visibility.Collapsed;
            FontSlider2.Visibility = Visibility.Collapsed;
            GeneralFont.Visibility = Visibility.Visible;
            checkBox.Content = "Individual View Font Settings";
        }

        private void FileIconButton(object sender, RoutedEventArgs e)
        {

        }

        private void LogRention(object sender, RoutedEventArgs e)
        {

        }


        private void KeepHistoryChecked(object sender, RoutedEventArgs e)
        {

        }

        private void KeepHistoryUnChecked(object sender, RoutedEventArgs e)
        {

        }

        private void LogRetentionChecked(object sender, RoutedEventArgs e)
        {
            dropDownMenuRetention.IsEnabled = true;
        }

        private void LogRetentionUnChecked(object sender, RoutedEventArgs e)
        {
            dropDownMenuRetention.IsEnabled = false;
        }

        private void LogLevelChecked(object sender, RoutedEventArgs e)
        {
            dropDownMenuLogLevel.IsEnabled = true;
        }

        private void LogLevelUnChecked(object sender, RoutedEventArgs e)
        {
            dropDownMenuLogLevel.IsEnabled= false;
        }
    }
}
