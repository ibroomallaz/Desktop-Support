using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Interfaces
{
    public interface ISearchableViewModel
    {
        Task OnSearchUpdated(string query);
    }
}
