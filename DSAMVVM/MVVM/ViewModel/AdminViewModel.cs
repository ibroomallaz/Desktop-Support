using DSAMVVM.Core.Utilities;
using System.Windows.Input;
using DSAMVVM.Core.Logging;

namespace DSAMVVM.MVVM.ViewModel
{
    public class AdminViewModel : ObservableObject
    {
        // ==========================================
        // 1. DEPARTMENT PROPERTIES
        // ==========================================
        private string _deptId;
        public string DeptId
        {
            get => _deptId;
            set { _deptId = value; OnPropertyChanged(nameof(DeptId)); }
        }

        private bool _supportKnown;
        public bool SupportKnown
        {
            get => _supportKnown;
            set { _supportKnown = value; OnPropertyChanged(nameof(SupportKnown)); }
        }

        private string _deptTeam;
        public string DeptTeam
        {
            get => _deptTeam;
            set { _deptTeam = value; OnPropertyChanged(nameof(DeptTeam)); }
        }

        private string _deptNotes;
        public string DeptNotes
        {
            get => _deptNotes;
            set { _deptNotes = value; OnPropertyChanged(nameof(DeptNotes)); }
        }

        // ==========================================
        // 2. SUPPORT TEAM PROPERTIES
        // ==========================================
        private string _teamName;
        public string TeamName
        {
            get => _teamName;
            set { _teamName = value; OnPropertyChanged(nameof(TeamName)); }
        }

        private string _managerName;
        public string ManagerName
        {
            get => _managerName;
            set { _managerName = value; OnPropertyChanged(nameof(ManagerName)); }
        }

        private string _managerNetId;
        public string ManagerNetId
        {
            get => _managerNetId;
            set { _managerNetId = value; OnPropertyChanged(nameof(ManagerNetId)); }
        }

        private string _supportNumber;
        public string SupportNumber
        {
            get => _supportNumber;
            set { _supportNumber = value; OnPropertyChanged(nameof(SupportNumber)); }
        }

        // ==========================================
        // 3. LINKS PROPERTIES
        // ==========================================
        private int _selectedLinkTypeIndex; // 0 = Common, 1 = Team
        public int SelectedLinkTypeIndex
        {
            get => _selectedLinkTypeIndex;
            set { _selectedLinkTypeIndex = value; OnPropertyChanged(nameof(SelectedLinkTypeIndex)); }
        }

        private string _linkTargetTeam;
        public string LinkTargetTeam
        {
            get => _linkTargetTeam;
            set { _linkTargetTeam = value; OnPropertyChanged(nameof(LinkTargetTeam)); }
        }

        private string _linkDisplayName;
        public string LinkDisplayName
        {
            get => _linkDisplayName;
            set { _linkDisplayName = value; OnPropertyChanged(nameof(LinkDisplayName)); }
        }

        private string _linkDescription;
        public string LinkDescription
        {
            get => _linkDescription;
            set { _linkDescription = value; OnPropertyChanged(nameof(LinkDescription)); }
        }

        private string _linkUrl;
        public string LinkUrl
        {
            get => _linkUrl;
            set { _linkUrl = value; OnPropertyChanged(nameof(LinkUrl)); }
        }

        // --- COMMANDS ---
        public ICommand SaveCommand { get; }

        public AdminViewModel()
        {
            // Default Values
            SupportKnown = true;
            SelectedLinkTypeIndex = 0;

            // Initialize Commands
            SaveCommand = new RelayCommand(_ => ExecuteSave());
        }

        private void ExecuteSave()
        {
            // TODO: Implement JSON saving logic here based on which tab is active
            // You might need an active tab property to know what to save

            Log.Info("Admin", "Save button clicked in Admin Panel.");
            UiNotify.Success("Data saved successfully.", showStatusBar: true);
        }
    }
}