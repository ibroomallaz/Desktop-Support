using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System.Diagnostics;

namespace DSAMVVM.MVVM.ViewModel
{
    public class UserViewModel(IADService adService, IDepartmentService deptService) : ObeservableObject, ISearchableViewModel
    {
        private readonly IADService _adService = adService;
        private readonly IDepartmentService _deptService = deptService;

        private ADUserInfo? _user;
        public ADUserInfo? User
        {
            get => _user;
            private set
            {
                _user = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(EduAffiliation));
                OnPropertyChanged(nameof(DepartmentName));
                OnPropertyChanged(nameof(DepartmentNumber));
                OnPropertyChanged(nameof(Division));
                OnPropertyChanged(nameof(License));
                OnPropertyChanged(nameof(Enabled));
            }
        }

        private string? _error;
        public string? Error
        {
            get => _error;
            private set
            {
                _error = value;
                OnPropertyChanged();
            }
        }

        private List<string>? _mimGroups;
        public List<string>? MimGroups
        {
            get => _mimGroups;
            private set
            {
                _mimGroups = value;
                OnPropertyChanged();
            }
        }

        private string? _departmentNotes;
        public string? DepartmentNotes
        {
            get => _departmentNotes;
            private set
            {
                _departmentNotes = value;
                OnPropertyChanged();
            }
        }

        private List<string>? _teamNames;
        public List<string>? TeamNames
        {
            get => _teamNames;
            private set
            {
                _teamNames = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private string _searchLog = string.Empty;
        public string SearchLog
        {
            get => _searchLog;
            private set
            {
                _searchLog = value;
                OnPropertyChanged();
            }
        }

        private void AppendLog(string message)
        {
            SearchLog += message + "\n";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        // Bindable AD properties
        public string? DisplayName => User?.DisplayName;
        public string? EduAffiliation => User?.EduAffiliation;
        public string? DepartmentName => User?.DepartmentName;
        public string? DepartmentNumber => User?.DepartmentNumber;
        public string? Division => User?.Division;
        public string? License => User?.License;
        public bool? Enabled => User?.Enabled;

        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;
            MimGroups = null;
            DepartmentNotes = null;
            TeamNames = null;
            User = null;
            SearchLog = string.Empty;

            AppendLog($"UserViewModel received search for '{context.Query}'");

            if (target != SearchTarget.User)
            {
                Error = "Invalid search target provided to UserViewModel.";
                AppendLog("Invalid search target for UserViewModel");
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Query))
            {
                AppendLog("Query was null or whitespace.");
                return;
            }

            try
            {
                IsLoading = true;
                AppendLog("Starting user search...");

                var result = await searchService.SearchAsync(context, target);
                User = result as ADUserInfo;

                if (User is null || !User.Exists)
                {
                    Error = User?.ErrorMessage ?? "User not found.";
                    AppendLog($"Search complete. User not found. Error: {Error}");
                    return;
                }

                AppendLog("AD User Lookup Result:");
                AppendLog($"  DisplayName:       {User.DisplayName}");
                AppendLog($"  Enabled:           {User.Enabled}");
                AppendLog($"  Exists:            {User.Exists}");
                AppendLog($"  DepartmentName:    {User.DepartmentName}");
                AppendLog($"  DepartmentNumber:  {User.DepartmentNumber}");
                AppendLog($"  EduAffiliation:    {User.EduAffiliation}");
                AppendLog($"  License:           {User.License}");
                AppendLog($"  Division:          {User.Division}");

                if (!string.IsNullOrWhiteSpace(User.DepartmentNumber))
                {
                    var dept = await _deptService.GetDepartmentAsync(User.DepartmentNumber);
                    if (dept != null)
                    {
                        DepartmentNotes = dept.Notes;

                        // v2 flattened: single Team string -> wrap into list for existing binding
                        var team = await _deptService.GetTeamAsync(dept.Number);
                        TeamNames = string.IsNullOrWhiteSpace(team) ? new List<string>() : new List<string> { team! };

                        // v2 flattened: single FileRepoPath
                        var repoPath = await _deptService.GetFileRepoPathAsync(dept.Number);

                        AppendLog($"Department Info for {dept.Number}:");
                        AppendLog($"  Notes:         {dept.Notes}");
                        AppendLog($"  SupportKnown:  {dept.SupportKnown}");
                        AppendLog($"  Team:          {(string.IsNullOrWhiteSpace(team) ? "None" : team)}");
                        AppendLog($"  FileRepoPath:  {(string.IsNullOrWhiteSpace(repoPath) ? "None" : repoPath)}");
                        AppendLog("Team Names from service: " + (TeamNames?.Count > 0 ? string.Join(", ", TeamNames) : "None"));
                    }
                    else
                    {
                        AppendLog("No department info found.");
                    }
                }
                else
                {
                    AppendLog("No department number provided.");
                }
            }
            catch (Exception ex)
            {
                Error = $"Search failed: {ex.Message}";
                AppendLog($"Exception during user search: {ex}");
            }
            finally
            {
                IsLoading = false;
                AppendLog("User search process completed.");
            }
        }

        public async Task<string?> LookupNameByID(string id)
        {
            return await _adService.LookupNameByEmployeeID(id);
        }

        public void ClearLog()
        {
            SearchLog = string.Empty;
        }

    }
}
