using System.Windows;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Navigation;
using DSAMVVM.Core.Enums;
using DSAMVVM.MVVM.ViewModel.Overlays;


namespace DSAMVVM.MVVM.View.Overlays
{
    public partial class QuickSearchOverlayView : Window
    {
        private readonly QuickSearchOverlayViewModel _viewModel;
        private bool _isClosing = false;

        public QuickSearchOverlayView(string capturedText, QuickSearchOverlayViewModel viewModel)
        {
            InitializeComponent();
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

        // --- RADICALLY SIMPLIFIED LINK ROUTING ---
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

        // --- KEYBOARD SHORTCUT ROUTING ---
        protected override async void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    await _viewModel.ExecuteInlineSearchAsync(AppView.Computer);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    await _viewModel.ExecuteInlineSearchAsync(AppView.Group, BtnGroupTarget);
                }
                else
                {
                    await _viewModel.ExecuteInlineSearchAsync(AppView.User);
                }

                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (e.SystemKey == Key.U) { await _viewModel.ExecuteInlineSearchAsync(AppView.User); e.Handled = true; }
                else if (e.SystemKey == Key.C) { await _viewModel.ExecuteInlineSearchAsync(AppView.Computer); e.Handled = true; }
                else if (e.SystemKey == Key.G) { await _viewModel.ExecuteInlineSearchAsync(AppView.Group, BtnGroupTarget); e.Handled = true; }
            }
        }

        // --- BUTTON CLICK ROUTING ---
        private async void BtnUser_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ExecuteInlineSearchAsync(AppView.User);
        }

        private async void BtnComputer_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ExecuteInlineSearchAsync(AppView.Computer);
        }

        private async void BtnGroup_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ExecuteInlineSearchAsync(AppView.Group, BtnGroupTarget);
        }
    }
}