using System.Windows;
using System.Windows.Input;
using DSAMVVM.Core.Enums;
using DSAMVVM.MVVM.ViewModel.Overlays;

namespace DSAMVVM.MVVM.View.Overlays
{
    public partial class QuickSearchOverlayView : Window
    {
        private readonly QuickSearchOverlayViewModel _viewModel;

        public QuickSearchOverlayView(string capturedText, QuickSearchOverlayViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.SearchText = capturedText.Trim();

            Loaded += (s, e) =>
            {
                SearchBox.Focus();
                SearchBox.CaretIndex = SearchBox.Text.Length;
            };

            this.Deactivated += (s, e) => this.Close();
        }

        private async void BtnUser_Click(object sender, RoutedEventArgs e) => await ExecuteTargetedSearch(AppView.User);
        private async void BtnComputer_Click(object sender, RoutedEventArgs e) => await ExecuteTargetedSearch(AppView.Computer);
        private async void BtnGroup_Click(object sender, RoutedEventArgs e) => await ExecuteTargetedSearch(AppView.Group);

        protected override async void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (e.SystemKey == Key.U) { await ExecuteTargetedSearch(AppView.User); e.Handled = true; }
                else if (e.SystemKey == Key.C) { await ExecuteTargetedSearch(AppView.Computer); e.Handled = true; }
                else if (e.SystemKey == Key.G) { await ExecuteTargetedSearch(AppView.Group); e.Handled = true; }
            }
        }

        private async Task ExecuteTargetedSearch(AppView targetCategory)
        {
            await _viewModel.ExecuteInlineSearchAsync(targetCategory);
        }
    }
}