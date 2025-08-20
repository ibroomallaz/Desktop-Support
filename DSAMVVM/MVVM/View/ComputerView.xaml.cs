using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DSAMVVM.MVVM.ViewModel;

namespace DSAMVVM.MVVM.View
{
    public partial class ComputerView : UserControl
    {
        private const double DefaultFontSize = 14.0;  // match your UserView default
        private const double MinFontSize = 10.0;
        private const double MaxFontSize = 36.0;

        private double _currentFontSize = DefaultFontSize;

        public ComputerView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure the FlowDocument has an initial font size
            ApplyFontSize(_currentFontSize);
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            // no-op for now; placeholder if you later hook global settings change events
        }

        // ---------------------------
        // Bottom bar button handlers
        // ---------------------------

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ComputerViewModel vm)
            {
                vm.ClearLog();
                // Reset internal pointer if you add any delta rendering here later.
            }
        }

        private void IncreaseFont_Click(object sender, RoutedEventArgs e)
        {
            var next = Math.Min(MaxFontSize, _currentFontSize + 2);
            if (Math.Abs(next - _currentFontSize) > double.Epsilon)
            {
                _currentFontSize = next;
                ApplyFontSize(_currentFontSize);
            }
        }

        private void DecreaseFont_Click(object sender, RoutedEventArgs e)
        {
            var next = Math.Max(MinFontSize, _currentFontSize - 2);
            if (Math.Abs(next - _currentFontSize) > double.Epsilon)
            {
                _currentFontSize = next;
                ApplyFontSize(_currentFontSize);
            }
        }

        private void ResetFont_Click(object sender, RoutedEventArgs e)
        {
            _currentFontSize = DefaultFontSize;
            ApplyFontSize(_currentFontSize);
        }

        // ---------------------------
        // Helpers
        // ---------------------------

        private void ApplyFontSize(double size)
        {
            var viewer = FindDescendant<FlowDocumentScrollViewer>(this);
            if (viewer == null) return;

            // If Behavior hasn't inserted a document yet, make a minimal one so the size applies.
            if (viewer.Document == null)
            {
                viewer.Document = new FlowDocument();
            }

            viewer.Document.FontSize = size;
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
    }
}
