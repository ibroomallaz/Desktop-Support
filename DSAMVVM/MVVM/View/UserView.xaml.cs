using DSAMVVM.MVVM.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DSAMVVM.MVVM.View
{
    public partial class UserView : UserControl
    {
        private UserViewModel? _vm;
        private FlowDocumentScrollViewer? _viewer;

        public UserView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _viewer = FindOutputViewer();
            HookVm(DataContext as UserViewModel);
            ApplyFontSizeFromVm();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            UnhookVm(_vm);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnhookVm(_vm);
            HookVm(e.NewValue as UserViewModel);
            ApplyFontSizeFromVm();
        }

        private void HookVm(UserViewModel? vm)
        {
            _vm = vm;
            _vm?.PropertyChanged += OnVmPropertyChanged;
        }

        private void UnhookVm(UserViewModel? vm)
        {
            vm?.PropertyChanged -= OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserViewModel.EffectiveFontSize))
                ApplyFontSizeFromVm();
        }

        private void ApplyFontSizeFromVm()
        {
            if (_vm == null) return;

            var viewer = _viewer ??= FindOutputViewer();
            if (viewer == null) return;

            viewer.Document ??= new FlowDocument();
            viewer.Document.FontSize = _vm.EffectiveFontSize;
        }

        private FlowDocumentScrollViewer? FindOutputViewer()
        {
            if (this.FindName("OutputViewer") is FlowDocumentScrollViewer named) return named;
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

        // Bottom bar actions delegate to the ViewModel
        private void ClearLog_Click(object sender, RoutedEventArgs e) => _vm?.ClearLog();
        private void IncreaseFont_Click(object sender, RoutedEventArgs e) => _vm?.AdjustFont(+1);
        private void DecreaseFont_Click(object sender, RoutedEventArgs e) => _vm?.AdjustFont(-1);
        private void ResetFont_Click(object sender, RoutedEventArgs e) => _vm?.ResetFont();

        private async void RefreshDept_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
                await _vm.RefreshDepartmentDataAsync();
        }
    }
}
