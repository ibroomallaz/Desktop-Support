using DSAMVVM.Core;
using DSAMVVM.MVVM.Model.AD;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public class GroupViewModel : ObeservableObject, ISearchableViewModel
    {
        private readonly IADService _ad;

        private ADGroupInfo? _group;
        public ADGroupInfo? Group
        {
            get => _group;
            private set
            {
                _group = value;
                OnPropertyChanged();
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

        private string? _selectedNetId;
        public string? SelectedNetId
        {
            get => _selectedNetId;
            set
            {
                _selectedNetId = value;
                OnPropertyChanged();
                _ = LoadMimGroupsForSelectedUserAsync();
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

        public GroupViewModel(IADService adService)
        {
            _ad = adService;
        }

        public async void OnSearchUpdated(string query)
        {
            Error = null;
            Group = null;
            MimGroups = null;
            SelectedNetId = null;

            if (string.IsNullOrWhiteSpace(query))
                return;

            var result = await _ad.GetGroupAsync(query);
            Group = result;

            if (!result.Exists)
            {
                Error = result.ErrorMessage ?? "Group not found.";
            }
        }

        private async Task LoadMimGroupsForSelectedUserAsync()
        {
            MimGroups = null;

            if (!string.IsNullOrWhiteSpace(SelectedNetId))
            {
                var groups = await _ad.GetMimGroupsAsync(SelectedNetId);
                MimGroups = groups?.Count > 0 ? groups : ["No MIM groups found."];
            }
        }
    }
}
/* XAML Bindigns
<!-- Error message -->
    <TextBlock Text="{Binding Error}" Foreground="Red" FontWeight="Bold" Margin="0,0,0,10" />

    <!-- Group members -->
    <TextBlock Text="Members:" FontWeight="Bold" />
    <ItemsControl ItemsSource="{Binding Group.GroupMembers}" />

    <!-- MIM Groups -->
    <TextBlock Text="MIM Groups:" FontWeight="Bold" Margin="0,10,0,0" />
    <ItemsControl ItemsSource="{Binding MimGroups}" />
*/