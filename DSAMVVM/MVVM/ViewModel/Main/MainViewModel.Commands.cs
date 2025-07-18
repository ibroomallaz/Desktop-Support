using DSAMVVM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public partial class MainViewModel
    {
        public RelayCommand HomeViewCommand { get; private set; }
        public RelayCommand UserCommand { get; private set; }
        public RelayCommand ComputerCommand { get; private set; }
        public RelayCommand GroupCommand { get; private set; }
        public RelayCommand EntraCommand { get; private set; }
        public RelayCommand LinksCommand { get; private set; }
        public RelayCommand AboutCommand { get; private set; }

        private void InitializeCommands()
        {
            HomeViewCommand = new RelayCommand(_ => CurrentView = HomeVM);
            UserCommand = new RelayCommand(_ => CurrentView = UserVM);
            ComputerCommand = new RelayCommand(_ => CurrentView = ComputerVM);
            GroupCommand = new RelayCommand(_ => CurrentView = GroupVM);
            EntraCommand = new RelayCommand(_ => CurrentView = EntraVM);
            LinksCommand = new RelayCommand(_ => CurrentView = LinksVM);
            AboutCommand = new RelayCommand(_ => CurrentView = AboutVM);
        }
    }
}
