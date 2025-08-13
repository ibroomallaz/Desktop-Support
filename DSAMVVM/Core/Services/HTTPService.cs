
using DSAMVVM.Core.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Services
{
    public class HttpService : IHttpService
    {
        public HttpClient Client { get; }

        public HttpService()
        {
            Client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopSupportApp/1.0");
        }

        public Task<string> GetStringAsync(string url) => Client.GetStringAsync(url);

        public async Task DownloadFileAsync(string url, string filePath)
        {
            using var stream = await Client.GetStreamAsync(url);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fs);
        }

        public bool TryOpenUrl(string target, out Exception? error)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }
    }
}
