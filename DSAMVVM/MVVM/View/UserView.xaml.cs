using System.Windows;
using System.Windows.Controls;
using DSAMVVM.MVVM.ViewModel;

namespace DSAMVVM.MVVM.View
{
    public partial class UserView : UserControl
    {
        public UserView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserViewModel vm)
            {
                // Optional: force initial log into box
               //  OutputBox.Text = vm.SearchLog;
            }
        }
    }
}
