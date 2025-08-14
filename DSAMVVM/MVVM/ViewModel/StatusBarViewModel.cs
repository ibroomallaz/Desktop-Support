using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSAMVVM.MVVM.ViewModel
{
    public class StatusBarViewModel : INotifyPropertyChanged
    {
        private readonly StatusBus _bus;

        public StatusMessage? CurrentStatusMessage => _bus.Current;

        public StatusBarViewModel(StatusBus bus)
        {
            _bus = bus;
            _bus.CurrentChanged += (_, __) => OnPropertyChanged(nameof(CurrentStatusMessage));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
