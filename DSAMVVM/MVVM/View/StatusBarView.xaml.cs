using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace DSAMVVM.MVVM.View
{
    public partial class StatusBarView : UserControl
    {
        public StatusBarView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<StatusBarViewModel>();
        }
    }
}
