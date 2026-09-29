namespace DSAMVVM.Core.Interfaces
{
    public interface IDeepLinkRoutingService
    {
        event Action<string, string>? NavigationRequested;
        Task<string> HandleLinkAsync(string url, string? contextNetId = null);
        void RequestNavigation(string targetView, string targetQuery);
    }
}
