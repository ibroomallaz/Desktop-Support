using DSAMVVM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public partial class MainViewModel : DSAMVVM.Core.ObeservableObject
{
    private string _searchQuery;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                (CurrentView as ISearchableViewModel)?.OnSearchUpdated(_searchQuery);
            }
        }
    }

    private object _currentView;
    public object CurrentView
    {
        get => _currentView;
        set
        {
            _currentView = value;
            OnPropertyChanged();

            // Optional: sync search query on view switch
            if (_searchQuery is not null)
                (value as ISearchableViewModel)?.OnSearchUpdated(_searchQuery);
        }
    }
}
