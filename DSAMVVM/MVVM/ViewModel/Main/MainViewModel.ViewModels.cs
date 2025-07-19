using DSAMVVM.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public partial class MainViewModel
    {
        public HomeViewModel HomeVM { get; private set; } = null;
        public ComputerViewModel ComputerVM { get; private set; } = null;
        public UserViewModel UserVM { get; private set; } = null;
        public GroupViewModel GroupVM { get; private set; } = null;
        public EntraViewModel EntraVM { get; private set; } = null;
        public LinksViewModel LinksVM { get; private set; } = null;
        public AboutViewModel AboutVM { get; private set; } = null;
        public DSAMVVM.MVVM.Model.IDepartmentService DeptService { get; private set; } = null!;

        private void InitializeViewModels()
        {
            HomeVM = new HomeViewModel();
            UserVM = new UserViewModel();
            ComputerVM = new ComputerViewModel();
            GroupVM = new GroupViewModel();
            EntraVM = new EntraViewModel();
            LinksVM = new LinksViewModel();
            AboutVM = new AboutViewModel();
            
        }
    }
}
