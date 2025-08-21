using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using System;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    public class SearchService : ISearchService
    {
        private readonly IADService _ad;

        public SearchService(IADService ad)
        {
            _ad = ad ?? throw new ArgumentNullException(nameof(ad));
        }

        public async Task<object?> SearchAsync(SearchContextDTO context, SearchTarget target)
        {
            if (string.IsNullOrWhiteSpace(context.Query))
                throw new ArgumentException("Search query cannot be empty.", nameof(context));

            return target switch
            {
                SearchTarget.User => await _ad.GetUserAsync(context.Query),
                SearchTarget.Computer => await _ad.GetComputerAsync(context.Query),
                SearchTarget.Group => await _ad.GetGroupAsync(context.Query),
                _ => throw new NotSupportedException($"Search not implemented for '{target}'.")
            };
        }
    }
}
