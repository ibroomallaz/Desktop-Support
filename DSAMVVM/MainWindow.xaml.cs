using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DSAMVVM
{
    public partial class MainWindow
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
            if (App.Settings.Ui.Tray.EnableTrayIcon && App.Settings.Ui.Tray.MinimizeToTray)
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
            if (App.Settings.Ui.Tray.EnableTrayIcon && App.Settings.Ui.Tray.CloseToTray)
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
            if (sender is not Button { ContextMenu: not null } btn) return;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
        
        

        // Show history dropdown when clicking in the search box
        private void SearchBox_ShowHistory(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox { Items.Count: > 0 } combo)
            {
                combo.IsDropDownOpen = true;
            }
        }

        // Close the dropdown as soon as typing starts to avoid double-Enter issue
        private void SearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is ComboBox { IsDropDownOpen: true } combo)
            {
                combo.IsDropDownOpen = false;
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                if (App.Settings.Ui.Tray.EnableTrayIcon && App.Settings.Ui.Tray.MinimizeToTray)
                {
                    WindowState = WindowState.Normal;
                    this.Hide();
                }
            }

            base.OnStateChanged(e);
        }
    }
}