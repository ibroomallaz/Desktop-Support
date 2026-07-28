using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Services.Status;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public class StatusBarViewModel : INotifyPropertyChanged
    {
        public StatusBus StatusBus { get; }
        public ICommand DismissCommand { get; }

        public StatusItem? CurrentStatusItem => StatusBus.Current;

        public StatusBarViewModel(StatusBus bus)
        {
            StatusBus = bus;

            //Dismiss wiring
            DismissCommand = new RelayCommand(_ => DismissCurrent(), _ => CurrentStatusItem != null);

            // Existing listener to refresh the UI when the bus updates
            StatusBus.CurrentChanged += (_, __) => OnPropertyChanged(nameof(CurrentStatusItem));
        }

        private void DismissCurrent()
        {
            var current = CurrentStatusItem;
            if (current != null && !string.IsNullOrEmpty(current.Key))
            {
                // Forcibly remove the item by its unique key
                StatusBus.RemoveByKey(current.Key);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}