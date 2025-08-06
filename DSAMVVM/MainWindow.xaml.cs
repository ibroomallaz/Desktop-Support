using DSAMVVM.MVVM.ViewModel;
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
            this.WindowState = WindowState.Minimized;
        }

        // Close window
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
    }
}
