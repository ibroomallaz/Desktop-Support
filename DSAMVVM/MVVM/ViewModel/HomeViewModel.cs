using System.Windows.Input;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;

namespace DSAMVVM.MVVM.ViewModel
{
    public class HomeViewModel : ObservableObject
    {
        // Callbacks provided by MainViewModel
        private readonly Action<string?>? _openUser;
        private readonly Action<string?>? _openComputer;
        private readonly Action? _goGroups;
        private readonly Action? _goEntra;
        private readonly Action? _goLinks;
        private readonly Action? _goAbout;

        // Header
        public string Title { get; } = $"Welcome, {IdentityUtility.GetFirstName()}";

        public string Subtitle { get; } = "Jump into common tasks.";
        public static string AppVersion => $"Version: {Globals.g_AppVersion}";

        // Quick-panel inputs (kept for your XAML bindings)
        private string _userQuery = "";
        public string UserQuery
        {
            get => _userQuery;
            set { _userQuery = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private string _computerQuery = "";
        public string ComputerQuery
        {
            get => _computerQuery;
            set { _computerQuery = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        // Commands (RelayCommand expects parameterized delegates in your project)
        public ICommand OpenUserCommand { get; }
        public ICommand OpenComputerCommand { get; }
        public ICommand GoGroupsCommand { get; }
        public ICommand GoEntraCommand { get; }
        public ICommand GoLinksCommand { get; }
        public ICommand GoAboutCommand { get; }

        public HomeViewModel(
            Action<string?>? openUser = null,
            Action<string?>? openComputer = null,
            Action? goGroups = null,
            Action? goEntra = null,
            Action? goLinks = null,
            Action? goAbout = null)
        {
            _openUser = openUser;
            _openComputer = openComputer;
            _goGroups = goGroups;
            _goEntra = goEntra;
            _goLinks = goLinks;
            _goAbout = goAbout;

            OpenUserCommand = new RelayCommand(
                _ => _openUser?.Invoke(UserQuery),
                _ => !string.IsNullOrWhiteSpace(UserQuery));

            OpenComputerCommand = new RelayCommand(
                _ => _openComputer?.Invoke(ComputerQuery),
                _ => !string.IsNullOrWhiteSpace(ComputerQuery));

            GoGroupsCommand = new RelayCommand(_ => _goGroups?.Invoke());
            GoEntraCommand = new RelayCommand(_ => _goEntra?.Invoke());
            GoLinksCommand = new RelayCommand(_ => _goLinks?.Invoke());
            GoAboutCommand = new RelayCommand(_ => _goAbout?.Invoke());
        }

        public static Task OnSearchUpdated(string query) => Task.CompletedTask; // no-op
    }
}
