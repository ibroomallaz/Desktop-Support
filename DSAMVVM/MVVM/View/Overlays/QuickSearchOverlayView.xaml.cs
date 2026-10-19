using DSAMVVM.Core.Enums;
using DSAMVVM.MVVM.View.Helpers;
using DSAMVVM.MVVM.ViewModel.Overlays;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;


namespace DSAMVVM.MVVM.View.Overlays
{
    public partial class QuickSearchOverlayView
    {
        private readonly QuickSearchOverlayViewModel _viewModel;
        private bool _isClosing;

        public QuickSearchOverlayView(string capturedText, QuickSearchOverlayViewModel viewModel)
        {
            InitializeComponent();
            SourceInitialized += (_, _) => WindowBackdropHelper.ApplyModernAttributes(this);

            _viewModel = viewModel;
            DataContext = _viewModel;

            // Capture all hyperlink clicks in this independent window
            this.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnHyperlinkClicked));

            // Wire up the close action from the ViewModel
            _viewModel.CloseAction = () =>
            {
                if (!_isClosing) this.Close();
            };

            // Let the ViewModel sanitize and load the text
            _viewModel.LoadCapturedText(capturedText);

            Loaded += (s, e) =>
            {
                SearchBox.Focus();
                SearchBox.CaretIndex = SearchBox.Text.Length;
            };

            // Safeguard for window closing
            this.Closing += (s, e) => _isClosing = true;

            this.Deactivated += (s, e) =>
            {
                if (!_isClosing)
                {
                    this.Close();
                }
            };
        }

        // ---  LINK ROUTING ---
        private async void OnHyperlinkClicked(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true; // Prevent default WPF behavior

            string url = e.Uri.OriginalString;

            // 1. Send the URL to the router
            await _viewModel.RouteLinkClickAsync(url);

            // 2. Wait 50ms for the Main Window to steal focus, then close ourselves
            await Task.Delay(50);
            if (!_isClosing) this.Close();
        }

        // Enables window dragging
        private void DragBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // --- KEYBOARD SHORTCUT ROUTING ---
        protected override async void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            AppView? targetView = (e.Key, Keyboard.Modifiers, e.SystemKey) switch
            {
                (Key.Enter, ModifierKeys.Shift, _)   => AppView.Computer,
                (Key.Enter, ModifierKeys.Control, _) => AppView.Group,
                (Key.Enter, ModifierKeys.None, _)    => AppView.User,
                (_, ModifierKeys.Alt, Key.U)         => AppView.User,
                (_, ModifierKeys.Alt, Key.C)         => AppView.Computer,
                (_, ModifierKeys.Alt, Key.G)         => AppView.Group,
                _                                    => null
            };

            if (targetView is not { } view) return;
            e.Handled = true;
            await TriggerSearchAsync(view);
        }

        // --- BUTTON CLICK ROUTING ---
        private async void BtnUser_Click(object sender, RoutedEventArgs e) => await TriggerSearchAsync(AppView.User);
        private async void BtnComputer_Click(object sender, RoutedEventArgs e) => await TriggerSearchAsync(AppView.Computer);
        private async void BtnGroup_Click(object sender, RoutedEventArgs e) => await TriggerSearchAsync(AppView.Group);

        private Task TriggerSearchAsync(AppView view)
        {
            var anchor = view == AppView.Group ? BtnGroupTarget : null;
            return _viewModel.ExecuteInlineSearchAsync(view, anchor);
        }
    }
}
