using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DSAMVVM.MVVM.View
{
    public partial class GroupView : UserControl
    {
        private const string ViewKey = "GroupView";
        private ISettingsService? _settingsSvc;
        private IOutputTextSettingsProvider? _notifier;

        public GroupView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? s, RoutedEventArgs e)
        {
            var sp = App.Services;
            _settingsSvc = sp.GetRequiredService<ISettingsService>();
            _notifier = sp.GetRequiredService<IOutputTextSettingsProvider>();
            _notifier.Changed += OnOutputFontSettingsChanged;

            ApplyEffectiveFontSize();

            if (DataContext is GroupViewModel vm)
            {
                vm.PropertyChanged -= VmOnPropertyChanged;
                vm.PropertyChanged += VmOnPropertyChanged;
            }
        }

        private void OnUnloaded(object? s, RoutedEventArgs e)
        {
            if (_notifier != null) _notifier.Changed -= OnOutputFontSettingsChanged;
            if (DataContext is GroupViewModel vm) vm.PropertyChanged -= VmOnPropertyChanged;
        }

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is GroupViewModel vm && e.PropertyName == nameof(GroupViewModel.SearchLog))
                Dispatcher.Invoke(InvalidateVisual); // FlowDocBinder will re-render bound text
        }

        private void OnOutputFontSettingsChanged(object? s, EventArgs e) => ApplyEffectiveFontSize();

        private void ApplyEffectiveFontSize()
        {
            if (_notifier == null) return;

            // Use the cached provider (coalesces duplicate reads across view + FlowDoc)
            double size = _notifier.GetFontSize(ViewKey);

            var viewer = FindOutputViewer();
            if (viewer == null) return;

            viewer.Document ??= new FlowDocument();
            viewer.Document.FontSize = size;
        }

        private FlowDocumentScrollViewer? FindOutputViewer()
        {
            return FindDescendant<FlowDocumentScrollViewer>(this);
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T t) return t;

            int count;
            try { count = VisualTreeHelper.GetChildrenCount(root); }
            catch { count = 0; }

            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var found = FindDescendant<T>(child);
                if (found != null) return found;
            }

            foreach (var obj in LogicalTreeHelper.GetChildren(root))
            {
                if (obj is DependencyObject d)
                {
                    var found = FindDescendant<T>(d);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GroupViewModel vm) vm.ClearLog();
        }

        private void IncreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFont(+1);
        private void DecreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFont(-1);
        private void ResetFont_Click(object sender, RoutedEventArgs e) => ResetFont();

        private void AdjustFont(int delta)
        {
            if (_settingsSvc == null || _notifier == null) return;
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;
            _ = _settingsSvc.AdjustOutputFontSize(s, perView ? ViewKey : null, delta, perView);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        private void ResetFont()
        {
            if (_settingsSvc == null || _notifier == null) return;
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;
            _settingsSvc.ResetOutputFontSize(s, perView ? ViewKey : null, perView, 14);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }
    }
}
