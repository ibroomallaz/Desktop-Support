using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSAMVVM.Core;

namespace DSAMVVM.MVVM.ViewModel
{
    public class EntraViewModel : ObeservableObject, ISearchableViewModel
    {
        public void OnSearchUpdated(string query)
        {
            // search logic here
        }
    }
}
