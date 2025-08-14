using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Interfaces
{

    public interface IHttpService : IDisposable
    {
        Task<string> GetStringAsync(string url, CancellationToken ct = default);

        Task<Stream> GetStreamAsync(string url, CancellationToken ct = default);

        Task DownloadFileAsync(string url, string filePath, CancellationToken ct = default);

        bool TryOpenUrl(string target, out Exception? error);
    }
}
