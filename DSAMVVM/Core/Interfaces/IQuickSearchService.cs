namespace DSAMVVM.Core.Interfaces
{
    public interface IQuickSearchService
    {
        event EventHandler<string> QuickSearchTriggered;

        void Start();
        void Stop();
    }
}