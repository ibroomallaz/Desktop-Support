using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DSAMVVM.Core
{
    public static class HTTPService
    {
        private static readonly HttpClient client = new();

        public static async Task DownloadFile(string url, string fileName)
        {
            using var stream = await client.GetStreamAsync(url);
            using var fs = new FileStream(fileName, FileMode.Create);
            await stream.CopyToAsync(fs);
        }

        public static bool TryOpenURL(string target, out Exception? error)
        {
            try
            {
                Process.Start(new ProcessStartInfo() { FileName = target, UseShellExecute = true });
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
