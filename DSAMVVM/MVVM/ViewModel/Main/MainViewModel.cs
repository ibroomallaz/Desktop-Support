using DSAMVVM.Core;
using DSAMVVM.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public partial class MainViewModel : DSAMVVM.Core.ObeservableObject
    {

        public MainViewModel()
        {
            try
            {
                InitializeStatusBar();
                InitializeInject();
                InitializeViewModels();
                InitializeCommands();
                

                CurrentView = HomeVM!;
            }
            catch (Exception ex)
            {
                StatusBar?.Report(StatusMessageFactory.Plain($"Initialization error: {ex.Message}", priority: 2, sticky: true));
            }
        }
    }
}
