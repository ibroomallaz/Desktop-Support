using DSAMVVM.MVVM.ViewModel.Dialogs;
using System.Windows;
using System.Windows.Input;

namespace DSAMVVM.MVVM.View.Dialogs
{

    public partial class VersionUpdateDialog : Window
    {
        public VersionUpdateDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is VersionUpdateDialogViewModel vm)
            {
                vm.RequestClose += () => Dispatcher.Invoke(Close);
                vm.RequestFocusNotes += () => Dispatcher.Invoke(() =>
                {
                    ChangelogScroll?.ScrollToTop();
                    ChangelogScroll?.Focus();
                });
            }

            var wa = SystemParameters.WorkArea;
            MaxWidth = Math.Min(wa.Width * 0.92, 1000);
            MaxHeight = wa.Height * 0.88;
            UpdateChangelogMaxHeight();
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateChangelogMaxHeight();

        private void UpdateChangelogMaxHeight()
        {
            var usable = Math.Max(0, ActualHeight - 220);
            ChangelogScroll.MaxHeight = Math.Max(140, usable * 0.6);
        }

        private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        // Helper to open as modal with inputs from the checker
        public static void ShowFor(
            Window? owner,
            string installed,
            string newVersion,
            string? location,
            string? changelog,
            bool isBeta)
        {
            var vm = new DSAMVVM.MVVM.ViewModel.Dialogs.VersionUpdateDialogViewModel(
                installedVersion: installed,
                newVersion: newVersion,
                isPreRelease: isBeta,                 // map to VM input
                downloadUrl: location,
                changelogText: changelog,
                changeLogUrl: DSAMVVM.MVVM.Model.Globals.g_ChangeLogURL);

            var dlg = new VersionUpdateDialog
            {
                Owner = owner ?? GetPreferredOwner(),
                DataContext = vm
            };
            dlg.ShowDialog();
        }


        private static Window? GetPreferredOwner()
        {
            var active = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            return active ?? Application.Current?.MainWindow;
        }
    }
}
