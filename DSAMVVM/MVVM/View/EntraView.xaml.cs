using System.Collections.Specialized;
using System.Windows;
using DSAMVVM.MVVM.ViewModel;

namespace DSAMVVM.MVVM.View
{
    public partial class EntraView
    {
        private EntraViewModel? _vm;

        public EntraView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm?.History != null)
            {
                _vm.History.CollectionChanged -= OnHistoryCollectionChanged;
            }

            _vm = e.NewValue as EntraViewModel;

            if (_vm?.History != null)
            {
                _vm.History.CollectionChanged += OnHistoryCollectionChanged;
            }
        }

        private void OnHistoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    HistoryScrollViewer?.ScrollToEnd();
                });
            }
        }
    }
}
