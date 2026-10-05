using System.Collections.Specialized;
using System.Windows;
using DSAMVVM.MVVM.ViewModel;

namespace DSAMVVM.MVVM.View
{
    public partial class GroupView
    {
        private GroupViewModel? _vm;

        public GroupView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            HookVm(DataContext as GroupViewModel);
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            UnhookVm(_vm);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnhookVm(_vm);
            HookVm(e.NewValue as GroupViewModel);
        }

        private void HookVm(GroupViewModel? vm)
        {
            _vm = vm;
            if (_vm != null)
            {
                _vm.History.CollectionChanged += OnHistoryCollectionChanged;
            }
        }

        private void UnhookVm(GroupViewModel? vm)
        {
            if (vm != null)
            {
                vm.History.CollectionChanged -= OnHistoryCollectionChanged;
            }
        }

        private void OnHistoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // Auto-scroll to the bottom of the feed so the newest card is in view
                Dispatcher.InvokeAsync(() =>
                {
                    HistoryScrollViewer.ScrollToBottom();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }
}
