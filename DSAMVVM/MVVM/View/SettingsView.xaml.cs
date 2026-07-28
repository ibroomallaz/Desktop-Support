using DSAMVVM.MVVM.ViewModel;
using System.Windows.Controls;

namespace DSAMVVM.MVVM.View
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext ??= new SettingsViewModel();
        }
    }
}