using DSAMVVM.MVVM.Model.Config;

namespace DSAMVVM.Core.Interfaces.Search
{
    public interface IQuickSearchService
    {
        event EventHandler<string> QuickSearchTriggered;

        void Start();
        void Stop();
        void Configure(QuickSearchSettings settings);
    }
}