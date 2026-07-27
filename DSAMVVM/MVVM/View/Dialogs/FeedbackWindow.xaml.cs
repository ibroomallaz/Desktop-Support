using System.Windows;
using System.Windows.Input;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Services.Graph;
using DSAMVVM.MVVM.ViewModel.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace DSAMVVM.MVVM.View.Dialogs
{
    public partial class FeedbackWindow : Window
    {
        private readonly FeedbackWindowViewModel _viewModel;

        public FeedbackWindow(int defaultIndex = 0)
        {
            InitializeComponent();

            var authService = App.Services.GetRequiredService<IAuthenticationService>();
            var routingService = App.Services.GetRequiredService<TeamsRoutingService>();
            var appStateService = App.Services.GetRequiredService<IApplicationStateService>();

            _viewModel = new FeedbackWindowViewModel(authService, routingService, appStateService)
            {
                SelectedFeedbackIndex = defaultIndex
            };

            // Bridges the ViewModel close request to the Window's DialogResult
            _viewModel.RequestClose += (s, result) => DialogResult = result;

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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}