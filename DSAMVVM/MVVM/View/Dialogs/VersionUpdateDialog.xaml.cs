using DSAMVVM.MVVM.ViewModel.Dialogs;
using System.Windows;
using System.Windows.Input;

namespace DSAMVVM.MVVM.View.Dialogs
{
    // Initializes the graphical components and handles dynamic sizing for the version update dialog
    public partial class VersionUpdateDialog : Window
    {
        public VersionUpdateDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        // Binds UI-specific actions and calculates maximum window dimensions based on the system work area
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is VersionUpdateDialogViewModel vm)
            {
                // Wire up the close request from the ViewModel
                vm.RequestClose += () => Dispatcher.Invoke(Close);

                // Wire up the focus request for the changelog
                vm.RequestFocusNotes += () => Dispatcher.Invoke(() =>
                {
                    ChangelogScroll?.ScrollToTop();
                    ChangelogScroll?.Focus();
                });
            }

            var wa = SystemParameters.WorkArea;
            MaxWidth = Math.Max(wa.Width * 0.92, 1000);
            MaxHeight = wa.Height * 0.88;
            UpdateChangelogMaxHeight();
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateChangelogMaxHeight();

        // Dynamically constrains the changelog scroll viewer height relative to the current window size
        private void UpdateChangelogMaxHeight()
        {
            // Adjusted offset slightly to account for the new Admin Warning border
            var usable = Math.Max(0, ActualHeight - 240);
            ChangelogScroll?.MaxHeight = Math.Max(140, usable * 0.6);
        }

        // Enables dragging the window by clicking and holding the custom title bar
        private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { /* Ignore potential move errors if window is closing */ }
            }
        }
    }
}