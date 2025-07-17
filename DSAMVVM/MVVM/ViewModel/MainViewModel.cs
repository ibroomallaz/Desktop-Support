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
        public RelayCommand UserCommand { get; set; }
        public HomeViewModel HomeVM { get; set; }

        public UserViewModel UserVM { get; set; }
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
            UserVM = new UserViewModel(); 
            CurrentView = HomeVM;
            HomeViewCommand = new RelayCommand(o => { 
                CurrentView = HomeVM;
            });
            UserCommand = new RelayCommand(o => {
                CurrentView = UserVM;
            });
        }
    }
}
