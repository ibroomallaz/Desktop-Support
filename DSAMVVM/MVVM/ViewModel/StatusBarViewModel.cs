using DSAMVVM.Core.Models;
using DSAMVVM.MVVM.Services.Status;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSAMVVM.MVVM.ViewModel
{
    public class StatusBarViewModel : INotifyPropertyChanged
    {
        public StatusBus StatusBus { get; }                     // expose the bus for simple XAML binding
        public StatusItem? CurrentStatusItem => StatusBus.Current;

        public StatusBarViewModel(StatusBus bus)
        {
            StatusBus = bus;
            StatusBus.CurrentChanged += (_, __) => OnPropertyChanged(nameof(CurrentStatusItem));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
