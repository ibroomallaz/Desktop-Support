using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public class ComputerViewModel(IADService adService) : ObeservableObject, ISearchableViewModel
    {
        private readonly IADService _ad = adService;

        private ADComputerInfo? _computer;
        public ADComputerInfo? Computer
        {
            get => _computer;
            private set
            {
                _computer = value;
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

        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;
            Computer = null;

            Debug.WriteLine($"[DEBUG] ComputerViewModel: Received search for '{context.Query}'");

            if (target != SearchTarget.Computer)
            {
                Error = "Invalid search target passed to ComputerViewModel.";
                Debug.WriteLine("[DEBUG] Invalid search target.");
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Query))
            {
                Debug.WriteLine("[DEBUG] Empty or whitespace query.");
                return;
            }

            try
            {
                IsLoading = true;
                Debug.WriteLine("[DEBUG] Starting computer search...");

                var result = await searchService.SearchAsync(context, target);
                Computer = result as ADComputerInfo;

                if (Computer is null || !Computer.Exists)
                {
                    Error = Computer?.ErrorMessage ?? "Computer not found.";
                    Debug.WriteLine($"[DEBUG] Search complete. Computer not found. Error: {Error}");
                    return;
                }

                // Output all fields from ADComputerInfo
                Debug.WriteLine("[DEBUG] AD Computer Lookup Result:");
                Debug.WriteLine($"  Name:              {Computer.Name}");
                Debug.WriteLine($"  Description:       {Computer.Description}");
                Debug.WriteLine($"  Operating System:  {Computer.OperatingSystem}");
                Debug.WriteLine($"  OUs:               {Computer.OUs}");
                Debug.WriteLine($"  Enabled:           {Computer.Enabled}");
                Debug.WriteLine($"  IsHybridGroup:     {Computer.IsHybridGroupMember}");
            }
            catch (Exception ex)
            {
                Error = $"Search failed: {ex.Message}";
                Debug.WriteLine($"[DEBUG] Exception during computer search: {ex}");
            }
            finally
            {
                IsLoading = false;
                Debug.WriteLine("[DEBUG] Computer search process completed.");
            }
        }
    }
}



/*
 * XAML Bindings:
 * <TextBlock Text="{Binding Computer.Name}" />
<TextBlock Text="{Binding Computer.OperatingSystem}" />
<TextBlock Text="{Binding Computer.Description}" />
<TextBlock Text="{Binding Computer.OUs}" />
<TextBlock Text="{Binding Error}" Foreground="Red" />
*/