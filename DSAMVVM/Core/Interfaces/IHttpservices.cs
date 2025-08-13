using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Interfaces
{
    public interface IHttpService
    {
        HttpClient Client { get; }                 // shared client
        Task<string> GetStringAsync(string url);   // simple wrapper
        Task DownloadFileAsync(string url, string filePath);
        bool TryOpenUrl(string target, out Exception? error);
    }
}
