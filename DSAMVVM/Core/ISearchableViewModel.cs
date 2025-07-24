using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.Core
{
    public interface ISearchableViewModel
    {
        void OnSearchUpdated(string query);
    }
}
