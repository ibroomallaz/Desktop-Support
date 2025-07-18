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
        public HomeViewModel HomeVM { get; private set; }
        public ComputerViewModel ComputerVM { get; private set; }
        public UserViewModel UserVM { get; private set; }
        public GroupViewModel GroupVM { get; private set; }
        public EntraViewModel EntraVM { get; private set; }
        public LinksViewModel LinksVM { get; private set; }
        public AboutViewModel AboutVM { get; private set; }
        public DSAMVVM.MVVM.Model.IDepartmentService DeptService { get; private set; }

        private void InitializeViewModels()
        {
            HomeVM = new HomeViewModel();
            UserVM = new UserViewModel();
            ComputerVM = new ComputerViewModel();
            GroupVM = new GroupViewModel();
            EntraVM = new EntraViewModel();
            LinksVM = new LinksViewModel();
            AboutVM = new AboutViewModel();

            DeptService = InjectStatus(status => new DepartmentService(status));
        }
    }
}
