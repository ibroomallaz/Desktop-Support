using DSAMVVM.Core;
using DSAMVVM.MVVM.Model;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public class StatusBarViewModel : INotifyPropertyChanged, IStatusReporter
    {
        private StatusMessage? _currentMessage;
        private CancellationTokenSource? _cts;

        public StatusMessage? CurrentStatusMessage => _currentMessage;

        public void Report(StatusMessage message, int timeoutMs = 5000)
        {
            if (_currentMessage == null || message.Priority >= _currentMessage.Priority)
            {
                _currentMessage = message;
                OnPropertyChanged(nameof(CurrentStatusMessage));

                if (!message.Sticky)
                {
                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    _ = AutoClearAsync(message, timeoutMs, _cts.Token);
                }
            }
        }

        private async Task AutoClearAsync(StatusMessage msg, int timeout, CancellationToken token)
        {
            try
            {
                await Task.Delay(timeout, token);
                if (_currentMessage == msg)
                {
                    _currentMessage = null;
                    OnPropertyChanged(nameof(CurrentStatusMessage));
                }
            }
            catch (TaskCanceledException) { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
