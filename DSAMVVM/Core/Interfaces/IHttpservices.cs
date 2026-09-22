using System.IO;
using System.Net.Http;

namespace DSAMVVM.Core.Interfaces
{
    public interface IHttpService : IDisposable
    {
        Task<string> GetStringAsync(string url, CancellationToken ct = default);
        Task<string> GetStringAsync(string url, TimeSpan timeout, CancellationToken ct = default);

        Task<Stream> GetStreamAsync(string url, CancellationToken ct = default);

        Task DownloadFileAsync(string url, string filePath, CancellationToken ct = default);
        Task DownloadFileAsync(string url, string filePath, TimeSpan timeout, CancellationToken ct = default);

        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default);
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct = default);

        bool TryOpenUrl(string target, out Exception? error);
    }
}
