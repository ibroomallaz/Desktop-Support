using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensibility;

namespace DSAMVVM.Core.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IPublicClientApplication _pca;
        internal string? _capturedAuthUri;

        public AuthenticationService()
        {
            var authorityUrl = $"{Globals.EntraInstanceUrl}{Globals.EntraTenantId}";

            var builder = PublicClientApplicationBuilder.Create(Globals.EntraClientId)
                .WithAuthority(authorityUrl)
                .WithRedirectUri(Globals.EntraRedirectUri)
                .WithLogging((level, message, containsPii) =>
                {
                    Log.Debug("MSAL", message);
                }, LogLevel.Info, enablePiiLogging: false);

            var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);
            builder.WithBroker(brokerOptions);

            _pca = builder.Build();
        }

        // Silent token cache verification with fallback to interactive browser flows
        public async Task<string> GetGraphAccessTokenAsync(string[] scopes)
        {
            Log.Info("AuthService", $"Extracting access token for scopes: {string.Join(", ", scopes)}");

            var accounts = await _pca.GetAccountsAsync();
            try
            {
                var silentResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();

                Log.Debug("AuthService", "Token extracted silently from modern WAM runtime profile.");
                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException ex)
            {
                Log.Warn("AuthService", $"Msal UI needed, initializing protocol redirect chain: {ex.Message}");
                return await AcquireTokenViaDeepLinkFallbackAsync(scopes);
            }
            catch (Exception ex)
            {
                Log.Error("AuthService", "Unexpected exception during silent token cache extraction loop.", ex);
                throw;
            }
        }

        // Interactive token acquisition sequence routing through the WAM broker or fallback web UI
        private async Task<string> AcquireTokenViaDeepLinkFallbackAsync(string[] scopes)
        {
            IntPtr windowHandle = IntPtr.Zero;

            // Extracts the native window handle from the primary WPF UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    windowHandle = new WindowInteropHelper(mainWindow).Handle;
                }
            });

            // Binds the interactive MSAL security dialog to the application's main window process
            var result = await _pca.AcquireTokenInteractive(scopes)
                .WithParentActivityOrWindow(windowHandle)
                .WithCustomWebUi(new DeepLinkWebUi(this))
                .ExecuteAsync();

            return result.AccessToken;
        }

        // Ingests the raw URL passed from single-instance deep link routing interceptions
        public void ProcessAuthRedirect(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                _capturedAuthUri = url;
                Log.Info("AuthService", "Authorization URI cleanly extracted from active link tracking handle.");
            }
        }

        // --- CUSTOM WEB UI BRIDGE ---
        private class DeepLinkWebUi(AuthenticationService parent) : ICustomWebUi
        {
            public async Task<Uri> AcquireAuthorizationCodeAsync(Uri authorizationUri, Uri redirectUri, CancellationToken cancellationToken)
            {
                parent._capturedAuthUri = null;

                Log.Info("AuthService", "Launching corporate Single Sign-On window inside user browser runtime.");
                Process.Start(new ProcessStartInfo
                {
                    FileName = authorizationUri.ToString(),
                    UseShellExecute = true
                });

                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), cancellationToken);
                while (parent._capturedAuthUri == null && !timeoutTask.IsCompleted)
                {
                    await Task.Delay(250, cancellationToken);
                }

                if (parent._capturedAuthUri == null)
                {
                    throw new TimeoutException("The technician SSO verification sequence timed out in the system browser.");
                }

                return new Uri(parent._capturedAuthUri);
            }
        }
    }
}