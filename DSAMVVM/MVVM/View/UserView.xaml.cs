using DSAMVVM.MVVM.ViewModel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DSAMVVM.MVVM.View
{
    public partial class UserView : UserControl
    {
        private const double DefaultFontSize = 14;
        private double _currentFontSize = DefaultFontSize;

        public UserView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserViewModel vm)
            {
                // Optional: uncomment to see what was in the log at load time
                // Debug.WriteLine("SearchLog at load:\n" + vm.SearchLog);

                OutputViewer.Document = new FlowDocument
                {
                    Foreground = Brushes.White
                };

                vm.PropertyChanged += Vm_PropertyChanged;
                AppendLogToFlow(vm.SearchLog);
            }
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is UserViewModel vm && e.PropertyName == nameof(UserViewModel.SearchLog))
            {
                Dispatcher.Invoke(() => AppendLogToFlow(vm.SearchLog));
            }
        }

        private void AppendLogToFlow(string log)
        {
            if (OutputViewer.Document == null) return;

            var doc = OutputViewer.Document;
            doc.Blocks.Clear();

            var lines = log.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var p = new Paragraph
                {
                    FontSize = _currentFontSize,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0)
                };

                if (line.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Error", StringComparison.OrdinalIgnoreCase))
                    p.Foreground = Brushes.Red;
                else if (line.Contains("Starting", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("completed", StringComparison.OrdinalIgnoreCase))
                    p.Foreground = Brushes.Green;
                else if (line.Contains("Department", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("Team", StringComparison.OrdinalIgnoreCase))
                    p.Foreground = Brushes.SkyBlue;
                else
                    p.Foreground = Brushes.White;

                p.Inlines.Add(new Run(line));
                doc.Blocks.Add(p);
            }
        }

        private void IncreaseFont_Click(object sender, RoutedEventArgs e)
        {
            _currentFontSize = Math.Min(_currentFontSize + 2, 24);
            if (DataContext is UserViewModel vm) AppendLogToFlow(vm.SearchLog);
        }

        private void DecreaseFont_Click(object sender, RoutedEventArgs e)
        {
            _currentFontSize = Math.Max(_currentFontSize - 2, 8);
            if (DataContext is UserViewModel vm) AppendLogToFlow(vm.SearchLog);
        }

        private void ResetFont_Click(object sender, RoutedEventArgs e)
        {
            _currentFontSize = DefaultFontSize;
            if (DataContext is UserViewModel vm) AppendLogToFlow(vm.SearchLog);
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserViewModel vm)
            {
                vm.ClearLog();
                OutputViewer.Document?.Blocks.Clear();
            }
        }
    }
}
