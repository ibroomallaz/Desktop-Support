using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DSAMVVM.Core
{
    internal class RelayCommand : ICommand
    {
        private Action<object> _execute;
        private Func<object, bool> _canExecute;
#pragma warning disable CS8612 // Nullability of reference types in type doesn't match implicitly implemented member.
        public event EventHandler CanExecuteChanged
#pragma warning restore CS8612 // Nullability of reference types in type doesn't match implicitly implemented member.
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public RelayCommand(Action<object> execute, Func<object, bool>? canExecute = null)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            _execute = execute;
#pragma warning disable CS8601 // Possible null reference assignment.
            _canExecute = canExecute;
#pragma warning restore CS8601 // Possible null reference assignment.
        }
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        public bool CanExecute(object parameter)
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        {
            return _canExecute == null || _canExecute(parameter);
        }
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        public void Execute(object parameter)
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
        {
            _execute(parameter);
        }
    }
}
