using DSAMVVM.Core;
using DSAMVVM.MVVM.Model.DSTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public class LinksViewModel : ObeservableObject
    {
        private readonly IStatusReporter _status;
        private readonly QuickLinks _quickLinks;

        private List<QuickLinks.Link> _commonLinks = new();
        public List<QuickLinks.Link> CommonLinks
        {
            get => _commonLinks;
            set { _commonLinks = value; OnPropertyChanged(); }
        }

        private List<QuickLinks.TeamLinkGroup> _teamLinks = new();
        public List<QuickLinks.TeamLinkGroup> TeamLinks
        {
            get => _teamLinks;
            set { _teamLinks = value; OnPropertyChanged(); }
        }

        public LinksViewModel(IStatusReporter status)
        {
            _status = status;
            _quickLinks = new QuickLinks(_status);
            _ = LoadLinksAsync();
        }

        private async Task LoadLinksAsync()
        {
            var data = await _quickLinks.GetQuickLinksDataAsync();
            if (data is not null)
            {
                CommonLinks = data.CommonLinks;
                TeamLinks = data.TeamLinks;
            }
        }
    }

}
