using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Services;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public class LinksViewModel : ObeservableObject
    {
        private readonly ILinksService _linksService;

        private List<Link> _commonLinks = [];
        public List<Link> CommonLinks
        {
            get => _commonLinks;
            set { _commonLinks = value; OnPropertyChanged(); }
        }

        private List<TeamLinkGroup> _teamLinks = [];
        public List<TeamLinkGroup> TeamLinks
        {
            get => _teamLinks;
            set { _teamLinks = value; OnPropertyChanged(); }
        }

        public LinksViewModel(ILinksService linksService)
        {
            _linksService = linksService;
            _ = LoadLinksAsync();
        }

        private async Task LoadLinksAsync()
        {
            var data = await _linksService.LoadLinksDataAsync();
            if (data is not null)
            {
                CommonLinks = data.CommonLinks;
                TeamLinks = data.TeamLinks;
            }
        }
    }
}
