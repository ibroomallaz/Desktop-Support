using DSAMVVM.Core.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace DSAMVVM.Core.Services
{
    // Shared HTTP utility. HttpClient is reused app-wide with connection pooling.
    public sealed class HttpService : IHttpService
    {
        private readonly HttpClient _client;
        private bool _disposed;

        public HttpService()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10), // Rotates connections periodically for DNS refresh
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                EnableMultipleHttp2Connections = true
            };

            _client = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopSupportApp/4.0");
        }

        public Task<string> GetStringAsync(string url, CancellationToken ct = default)
            => GetStringAsync(url, TimeSpan.FromSeconds(15), ct);

        public async Task<string> GetStringAsync(string url, TimeSpan timeout, CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            return await _client.GetStringAsync(url, cts.Token);
        }

        public Task<Stream> GetStreamAsync(string url, CancellationToken ct = default)
            => _client.GetStreamAsync(url, ct);

        public Task DownloadFileAsync(string url, string filePath, CancellationToken ct = default)
            => DownloadFileAsync(url, filePath, TimeSpan.FromSeconds(60), ct);

        public async Task DownloadFileAsync(string url, string filePath, TimeSpan timeout, CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = filePath + ".tmp";
            var succeeded = false;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                using var resp = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                resp.EnsureSuccessStatusCode();

                await using var src = await resp.Content.ReadAsStreamAsync(cts.Token);
                await using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    await src.CopyToAsync(dst, cts.Token);

                if (File.Exists(filePath))
                    File.Replace(tmp, filePath, destinationBackupFileName: null);
                else
                    File.Move(tmp, filePath);

                succeeded = true;
            }
            catch
            {
                if (File.Exists(tmp)) File.Delete(tmp);
                throw;
            }
            finally
            {
                if (!succeeded && File.Exists(tmp)) File.Delete(tmp);
            }
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
            => _client.SendAsync(request, ct);

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            return await _client.SendAsync(request, cts.Token);
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

        // Dispose pattern for a sealed type
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _client.Dispose();
            }
            _disposed = true;
        }
    }
}
