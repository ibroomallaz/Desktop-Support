namespace DSAMVVM.Core.Interfaces
{
    public interface IDeepLinkRoutingService
    {
        Task<string> HandleLinkAsync(string url, string? contextNetId = null);
    }
}