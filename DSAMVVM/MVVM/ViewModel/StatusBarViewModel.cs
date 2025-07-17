using DSAMVVM.Core;
using DSAMVVM.MVVM.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public class StatusBarViewModel : INotifyPropertyChanged
    {
        private StatusMessage? _currentMessage;
        private CancellationTokenSource? _cts;

        public string Message => _currentMessage?.Message ?? "";

        public void Report(StatusMessage message, int timeoutMs = 5000)
        {
            if (_currentMessage == null || message.Priority >= _currentMessage.Priority)
            {
                _currentMessage = message;
                OnPropertyChanged(nameof(Message));

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
                    OnPropertyChanged(nameof(Message));
                }
            }
            catch (TaskCanceledException) { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


}
