using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

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
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            SearchLog += line + "\n";
            Debug.WriteLine(line);
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
                AppendLog($"  Name:              {User.Name}");
                AppendLog($"  DisplayName:       {User.DisplayName}");
                AppendLog($"  Enabled:           {User.Enabled}");
                AppendLog($"  Exists:            {User.Exists}");
                AppendLog($"  DepartmentName:    {User.DepartmentName}");
                AppendLog($"  DepartmentNumber:  {User.DepartmentNumber}");
                AppendLog($"  EduAffiliation:    {User.EduAffiliation}");
                AppendLog($"  License:           {User.License}");
                AppendLog($"  Division:          {User.Division}");

                MimGroups = await _adService.GetMimGroupsAsync(context.Query);
                AppendLog("MIM Groups: " + (MimGroups?.Count > 0 ? string.Join(", ", MimGroups) : "None"));

                if (!string.IsNullOrWhiteSpace(User.DepartmentNumber))
                {
                    var dept = await _deptService.GetDepartmentAsync(User.DepartmentNumber);
                    if (dept != null)
                    {
                        DepartmentNotes = dept.Notes;
                        TeamNames = await _deptService.GetTeamNamesAsync(dept.Number);

                        AppendLog($"Department Info for {dept.Number}:");
                        AppendLog($"  Notes:         {dept.Notes}");
                        AppendLog($"  SupportKnown:  {dept.SupportKnown}");
                        AppendLog($"  SplitSupport:  {dept.SplitSupport}");
                        AppendLog($"  Teams:         {(dept.Teams?.Count > 0 ? string.Join(", ", dept.Teams.Select(t => t.Name)) : "None")}");
                        AppendLog($"  FileRepos:     {(dept.FileRepos?.Count > 0
                                    ? string.Join(", ", dept.FileRepos.Select(fr => fr.Exists
                                        ? $"{fr.Location ?? "(unknown)"} (Exists)"
                                        : $"{fr.Location ?? "(unknown)"} (Missing)"))
                                    : "None")}");



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
    }
}




/* Xaml Bindings:
<TextBlock Text="{Binding User.DisplayName}" />
<TextBlock Text="{Binding User.DepartmentName}" />
<TextBlock Text="{Binding User.DepartmentNumber}" />
<TextBlock Text="{Binding User.EduAffiliation}" />
<TextBlock Text="{Binding User.Division}" />
<TextBlock Text="{Binding User.License}" />
<TextBlock Text="{Binding User.Enabled}" />
<TextBlock Text="{Binding Error}" Foreground="Red" />
<ItemsControl ItemsSource="{Binding User.MimGroupsList}" />
<TextBlock Text="{Binding DepartmentNotes}" />
<ItemsControl ItemsSource="{Binding TeamNames}" />
*/
