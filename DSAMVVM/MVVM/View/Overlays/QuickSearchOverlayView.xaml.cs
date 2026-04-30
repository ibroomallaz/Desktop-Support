using System.Windows;
using System.Windows.Input;

namespace DSAMVVM.MVVM.View.Overlays
{
    public partial class QuickSearchOverlayView : Window
    {
        public QuickSearchOverlayView(string capturedText)
        {
            InitializeComponent();

            // Inject the text grabbed from the stealth copy
            SearchBox.Text = capturedText.Trim();

            // Auto-focus the text box when the window opens
            Loaded += (s, e) =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            };

            //Auto-close if the user clicks away or the app loses focus
            this.Deactivated += (s, e) => this.Close();
        }

        // Dismiss the overlay easily
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                this.Close();
            }
            else if (e.Key == Key.Enter)
            {
                // TODO: Execute your actual search logic here
                MessageBox.Show($"Searching for: {SearchBox.Text}");
                this.Close();
            }
        }
    }
}