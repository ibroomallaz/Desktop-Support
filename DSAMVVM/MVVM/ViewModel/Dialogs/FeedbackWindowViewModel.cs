using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSAMVVM.MVVM.ViewModel.Dialogs
{
    public sealed class FeedbackWindowViewModel : INotifyPropertyChanged
    {
        private int _selectedFeedbackIndex;
        private string _detailsText = string.Empty;

        public int SelectedFeedbackIndex
        {
            get => _selectedFeedbackIndex;
            set { _selectedFeedbackIndex = value; OnPropertyChanged(); }
        }

        public string DetailsText
        {
            get => _detailsText;
            set
            {
                _detailsText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSubmit));
            }
        }

        // Returns true only if text contains non-whitespace characters
        public bool CanSubmit => !string.IsNullOrWhiteSpace(DetailsText);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}