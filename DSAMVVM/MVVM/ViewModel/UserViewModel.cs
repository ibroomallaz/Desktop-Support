using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System.Collections.Generic;
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

                // Notify bindings for dependent properties
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

        // Individual bindable properties
        public string? DisplayName => User?.DisplayName;
        public string? EduAffiliation => User?.EduAffiliation;
        public string? DepartmentName => User?.DepartmentName;
        public string? DepartmentNumber => User?.DepartmentNumber;
        public string? Division => User?.Division;
        public string? License => User?.License;
        public bool? Enabled => User?.Enabled;

        public async void OnSearchUpdated(string query)
        {
            Error = null;
            MimGroups = null;
            User = null;

            if (string.IsNullOrWhiteSpace(query))
                return;

            var userInfo = await _adService.GetUserAsync(query);
            User = userInfo;

            if (!userInfo.Exists)
            {
                Error = userInfo.ErrorMessage ?? "User not found.";
                return;
            }

            var groups = await _adService.GetMimGroupsAsync(query);
            MimGroups = groups;
        }

        public async Task<string?> LookupNameByID(string id)
        {
            return await _adService.LookupNameByEmployeeID(id);
        }
    }
}

/* Xaml Bindings:
 * <TextBlock Text="{Binding User.DisplayName}" />
<TextBlock Text="{Binding User.DepartmentName}" />
<TextBlock Text="{Binding User.DepartmentNumber}" />
<TextBlock Text="{Binding User.EduAffiliation}" />
<TextBlock Text="{Binding User.Division}" />
<TextBlock Text="{Binding User.License}" />
<TextBlock Text="{Binding User.Enabled}" />
<TextBlock Text="{Binding Error}" Foreground="Red" />

<ItemsControl ItemsSource="{Binding User.MimGroupsList}" />
*/