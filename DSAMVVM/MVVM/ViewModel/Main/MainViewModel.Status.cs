using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public partial class MainViewModel
    {
        public StatusBarViewModel StatusBar { get; private set; } = null;

        public void InitializeStatusBar()
        {
            StatusBar = new StatusBarViewModel();
        }
    }
}
