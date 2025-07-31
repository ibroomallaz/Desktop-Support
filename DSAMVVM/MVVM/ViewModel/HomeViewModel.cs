using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public class HomeViewModel: ObeservableObject, ISearchableViewModel
    {
        public Task OnSearchUpdated(string query)
        {
            // search logic here
            return Task.CompletedTask;
        }
        public string AppVersion => $"Version: {Globals.g_AppVersion}";
    }
}
