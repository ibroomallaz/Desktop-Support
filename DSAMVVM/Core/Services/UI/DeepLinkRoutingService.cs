using DSAMVVM.Core.Logging;

namespace DSAMVVM.Core.Services.UI
{
    public class DeepLinkRoutingService : IDeepLinkRoutingService
    {
        public event Action<string, string>? NavigationRequested;

        public void RequestNavigation(string targetView, string targetQuery)
        {
            NavigationRequested?.Invoke(targetView, targetQuery);
        }

        public Task<string> HandleLinkAsync(string url, string? contextNetId = null)
        {
            if (string.IsNullOrWhiteSpace(url)) return Task.FromResult(string.Empty);

            // --- TRIAGE LOGGING ---
            Log.Info("LinkRouter", $"Deep link received: '{url}'");

            try
            {
                string? navPayload = null;
                if (url.StartsWith("dsa://nav/", StringComparison.OrdinalIgnoreCase))
                {
                    navPayload = url["dsa://nav/".Length..];
                }
                else if (url.StartsWith("app://", StringComparison.OrdinalIgnoreCase))
                {
                    navPayload = url["app://".Length..];
                }

                if (navPayload != null)
                {
                    var trimmed = navPayload.TrimStart('/');
                    string targetView = trimmed;
                    string targetQuery = string.Empty;

                    int qIdx = trimmed.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        targetView = trimmed[..qIdx];
                        targetQuery = trimmed[(qIdx + 1)..];
                    }
                    else
                    {
                        var parts = trimmed.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0) targetView = parts[0];
                        if (parts.Length > 1) targetQuery = parts[1];
                    }

                    Log.Info("LinkRouter", $"Parsed navigation link: View={targetView}, Query={targetQuery}");

                    // Fire the event that MainViewModel and QuickSearchOverlayViewModel listen to
                    NavigationRequested?.Invoke(targetView, targetQuery);

                    Log.Debug("LinkRouter", "NavigationRequested event successfully invoked.");
                    return Task.FromResult(string.Empty);
                }

                Log.Warn("LinkRouter", $"Unrecognized deep link format: '{url}'");
                return Task.FromResult(string.Empty);
            }
            catch (Exception ex)
            {
                Log.Error("LinkRouter", $"Failed to process deep link '{url}'", ex);
                return Task.FromResult(string.Empty);
            }
        }
    }
}
