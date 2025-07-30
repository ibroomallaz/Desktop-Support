using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
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

        public async void OnSearchUpdated(string query)
        {
            Error = null;
            Computer = null;

            if (string.IsNullOrWhiteSpace(query))
                return;

            var computerInfo = await _ad.GetComputerAsync(query);
            Computer = computerInfo;

            if (!computerInfo.Exists)
                Error = computerInfo.ErrorMessage ?? "Computer not found.";
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