using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;


namespace DSAMVVM.MVVM.ViewModel
{
    public class HomeViewModel: ObeservableObject
    {
        public static Task OnSearchUpdated(string query)
        {
            // search logic here
            return Task.CompletedTask;
        }
        public static string AppVersion => $"Version: {Globals.g_AppVersion}";
    }
}
