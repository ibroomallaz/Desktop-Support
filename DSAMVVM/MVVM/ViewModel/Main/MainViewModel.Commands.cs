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
        public RelayCommand HomeViewCommand { get; private set; } = null;
        public RelayCommand UserCommand { get; private set; } = null;
        public RelayCommand ComputerCommand { get; private set; } = null;
        public RelayCommand GroupCommand { get; private set; } = null;
        public RelayCommand EntraCommand { get; private set; } = null;
        public RelayCommand LinksCommand { get; private set; } = null;
        public RelayCommand AboutCommand { get; private set; } = null;

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
