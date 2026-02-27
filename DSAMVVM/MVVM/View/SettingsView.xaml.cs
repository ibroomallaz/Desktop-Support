using System.Windows.Controls;
using DSAMVVM.MVVM.ViewModel;

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