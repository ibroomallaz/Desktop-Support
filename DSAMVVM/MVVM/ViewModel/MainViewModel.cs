using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSAMVVM.Core;

namespace DSAMVVM.MVVM.ViewModel
{
    class MainViewModel : DSAMVVM.Core.ObeservableObject
    {
        public RelayCommand HomeViewCommand { get; set; }
        public RelayCommand Text2ViewCommand { get; set; }
        public HomeViewModel HomeVM { get; set; }

        public Text2ViewModel Text2VM { get; set; }
        private object _currentView;

        public object CurrentView
        {
            get { return _currentView; }
            set { _currentView = value;
                OnProptertyChanged();
            }
        }

        public MainViewModel() 
        { 
            HomeVM = new HomeViewModel();
            Text2VM = new Text2ViewModel(); 
            CurrentView = HomeVM;
            HomeViewCommand = new RelayCommand(o => { 
                CurrentView = HomeVM;
            });
            Text2ViewCommand = new RelayCommand(o => {
                CurrentView = Text2VM;
            });
        }
    }
}
