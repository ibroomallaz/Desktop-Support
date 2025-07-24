using DSAMVVM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public partial class MainViewModel : DSAMVVM.Core.ObeservableObject
    {
        private string? _searchQuery;
        public string? SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    if (!string.IsNullOrWhiteSpace(_searchQuery) &&
                    CurrentView is ISearchableViewModel searchable)
                    {
                        searchable.OnSearchUpdated(_searchQuery);
                    }
                }
            }
        }

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;
                    OnPropertyChanged();

                    if (!string.IsNullOrWhiteSpace(_searchQuery) &&
                        value is ISearchableViewModel searchable)
                    {
                        searchable.OnSearchUpdated(_searchQuery);
                    }
                }
            }
        }
    }
}
