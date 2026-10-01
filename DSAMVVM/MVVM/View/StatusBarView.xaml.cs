using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;

namespace DSAMVVM.MVVM.View
{
    public partial class StatusBarView
    {
        public StatusBarView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<StatusBarViewModel>();
        }
    }
}
