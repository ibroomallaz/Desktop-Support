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
        public RelayCommand ComputerCommand { get; set; }
        public RelayCommand GroupCommand { get; set; }
        public RelayCommand EntraCommand { get; set; }
        public RelayCommand LinksCommand { get; set; }
        public RelayCommand AboutCommand { get; set; }
        public HomeViewModel HomeVM { get; set; }
        public ComputerViewModel ComputerVM { get; set; }

        public UserViewModel UserVM { get; set; }
        public GroupViewModel GroupVM { get; set; }
        public EntraViewModel EntraVM { get; set; }
        public LinksViewModel LinksVM { get; set; }
        public AboutViewModel AboutVM { get; set; }
        public StatusBarViewModel StatusBar { get; }


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
            ComputerVM = new ComputerViewModel();
            GroupVM = new GroupViewModel();
            EntraVM = new EntraViewModel();
            LinksVM = new LinksViewModel();
            AboutVM = new AboutViewModel();
            StatusBar = new StatusBarViewModel();
            CurrentView = HomeVM;
            HomeViewCommand = new RelayCommand(o => { 
                CurrentView = HomeVM;
            });
            UserCommand = new RelayCommand(o => {
                CurrentView = UserVM;
            });
            ComputerCommand = new RelayCommand(o => {
                CurrentView = ComputerVM;
            });
            GroupCommand = new RelayCommand(o => {
                CurrentView = GroupVM;
            });
            EntraCommand = new RelayCommand(o => {
                CurrentView = EntraVM;
            });
            LinksCommand = new RelayCommand(o => {
                CurrentView = LinksVM;
            });
            AboutCommand = new RelayCommand(o => {
                CurrentView = AboutVM;
            });
        }
    }
}
