using DSAMVVM.MVVM.ViewModel.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DSAMVVM.MVVM.View.Dialogs
{
    public record FeedbackResult(string Type, string Text);

    public partial class FeedbackWindow : Window
    {
        private readonly FeedbackWindowViewModel _viewModel;
        public FeedbackResult? Result { get; private set; }

        public FeedbackWindow(int defaultIndex = 0)
        {
            InitializeComponent();

            _viewModel = new FeedbackWindowViewModel
            {
                SelectedFeedbackIndex = defaultIndex
            };

            DataContext = _viewModel;
            DetailsTextBox.Focus();
        }

        private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.CanSubmit) return; // Safety block

            var selectedItem = (ComboBoxItem)FeedbackTypeComboBox.SelectedItem;
            string feedbackType = selectedItem.Content.ToString() ?? "Bug";

            Result = new FeedbackResult(feedbackType, _viewModel.DetailsText.Trim());
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}