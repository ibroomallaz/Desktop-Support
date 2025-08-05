using System.Threading.Tasks;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;

namespace DSAMVVM.MVVM.ViewModel
{
    public class EntraViewModel : ObeservableObject
    {
        public async Task OnSearchUpdated(string query)
        {
            // Placeholder for future Entra ID search logic
            await Task.CompletedTask;
        }
    }
}

