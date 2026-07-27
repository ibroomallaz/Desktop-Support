using DSAMVVM.MVVM.View.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DSAMVVM
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Allow dragging the window from the title bar
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        // Minimize window
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            if (App.Settings != null && App.Settings.Ui.Tray.EnableTrayIcon && App.Settings.Ui.Tray.MinimizeToTray)
            {
                this.Hide();
            }
            else
            {
                this.WindowState = WindowState.Minimized;
            }
        }

        // Close window
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (App.Settings != null && App.Settings.Ui.Tray.EnableTrayIcon && App.Settings.Ui.Tray.CloseToTray)
            {
                this.Hide();
            }
            else
            {
                this.Close();
            }
        }

        // Opens the dropdown context menu on left-click of the warning icon
        private void FeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        // Opens feedback window pre-selected to "Report a Bug" (Index 0)
        private void ReportBug_Click(object sender, RoutedEventArgs e)
        {
            OpenFeedbackDialog(0);
        }

        // Opens feedback window pre-selected to "Request a Feature" (Index 1)
        private void MakeSuggestion_Click(object sender, RoutedEventArgs e)
        {
            OpenFeedbackDialog(1);
        }

        // Shared helper to launch the dialog modal
        private void OpenFeedbackDialog(int typeIndex)
        {
            var dialog = new FeedbackWindow(typeIndex)
            {
                Owner = this
            };

            // ShowDialog() will return true if it successfully submitted, or false if canceled.
            if (dialog.ShowDialog() == true)
            {
                System.Diagnostics.Debug.WriteLine($"[Feedback] Successfully submitted feedback of type index {typeIndex}.");
            }
        }

        // Show history dropdown when clicking in the search box
        private void SearchBox_ShowHistory(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox combo && combo.Items.Count > 0)
            {
                combo.IsDropDownOpen = true;
            }
        }

        // Close the dropdown as soon as typing starts to avoid double-Enter issue
        private void SearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is ComboBox combo && combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = false;
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                if (App.Settings != null && App.Settings.Ui.Tray.EnableTrayIcon && App.Settings.Ui.Tray.MinimizeToTray)
                {
                    WindowState = WindowState.Normal;
                    this.Hide();
                }
            }

            base.OnStateChanged(e);
        }
    }
}