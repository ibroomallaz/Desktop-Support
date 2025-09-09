using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DSAMVVM.MVVM.ViewModel;

namespace DSAMVVM.MVVM.View
{
    public partial class ComputerView : UserControl
    {
        private ComputerViewModel? _vm;

        public ComputerView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            AttachVm(DataContext as ComputerViewModel);
            ApplyEffectiveFontSize();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            DetachVm();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DetachVm();
            AttachVm(e.NewValue as ComputerViewModel);
            ApplyEffectiveFontSize();
        }

        private void AttachVm(ComputerViewModel? vm)
        {
            _vm = vm;
            if (_vm != null)
                _vm.PropertyChanged += VmOnPropertyChanged;
        }

        private void DetachVm()
        {
            if (_vm != null)
                _vm.PropertyChanged -= VmOnPropertyChanged;
            _vm = null;
        }

        private void VmOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ComputerViewModel.EffectiveOutputFontSize))
                ApplyEffectiveFontSize();
        }

        private void ApplyEffectiveFontSize()
        {
            if (_vm == null) return;

            var viewer = FindOutputViewer();
            if (viewer == null) return;

            viewer.Document ??= new FlowDocument();
            viewer.Document.FontSize = _vm.EffectiveOutputFontSize;
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

        // --- bottom bar buttons (thin wrappers -> VM commands) ---

        private void ClearLog_Click(object sender, RoutedEventArgs e)
            => _vm?.ClearLogCommand.Execute(null);

        private void IncreaseFont_Click(object sender, RoutedEventArgs e)
            => _vm?.IncreaseFontCommand.Execute(null);

        private void DecreaseFont_Click(object sender, RoutedEventArgs e)
            => _vm?.DecreaseFontCommand.Execute(null);

        private void ResetFont_Click(object sender, RoutedEventArgs e)
            => _vm?.ResetFontCommand.Execute(null);
    }
}
