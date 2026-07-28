using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensibility;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace DSAMVVM.Core.Services.Graph
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IPublicClientApplication _pca;
        private readonly TeamsRoutingService _routingService;
        internal string? _capturedAuthUri;
        private bool _isAuthenticated;

        public event Action<bool>? AuthenticationStateChanged;

        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            private set
            {
                if (_isAuthenticated != value)
                {
                    _isAuthenticated = value;

                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AuthenticationStateChanged?.Invoke(_isAuthenticated);
                        });
                    }
                    else
                    {
                        AuthenticationStateChanged?.Invoke(_isAuthenticated);
                    }
                }
            }
        }

        public AuthenticationService(TeamsRoutingService routingService)
        {
            _routingService = routingService;
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

        private void ExtractAndApplyRouting(AuthenticationResult result)
        {
            if (result?.ClaimsPrincipal?.Claims != null)
            {
                _routingService.InitializeFromClaims(result.ClaimsPrincipal.Claims);
                Log.Info("AuthService", "Teams routing configuration extracted and applied from ID Token.");
            }
        }

        // Silent authentication check for UI state binding prior to Graph execution
        public async Task<bool> ValidateAuthenticationAsync(string[] scopes)
        {
            var accounts = await _pca.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                IsAuthenticated = false;
                return false;
            }

            try
            {
                var silentResult = await _pca.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync();

                ExtractAndApplyRouting(silentResult);
                IsAuthenticated = !string.IsNullOrEmpty(silentResult?.AccessToken);
                return IsAuthenticated;
            }
            catch (MsalUiRequiredException)
            {
                IsAuthenticated = false;
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("AuthService", "Exception during silent authentication state check.", ex);
                IsAuthenticated = false;
                return false;
            }
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

                ExtractAndApplyRouting(silentResult);
                Log.Debug("AuthService", "Token extracted silently from modern WAM runtime profile.");

                IsAuthenticated = true;
                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException ex)
            {
                Log.Warn("AuthService", $"Msal UI needed, initializing protocol redirect chain: {ex.Message}");
                return await AcquireTokenInteractiveAsync(scopes);
            }
            catch (Exception ex)
            {
                Log.Error("AuthService", "Unexpected exception during silent token cache extraction loop.", ex);
                throw;
            }
        }

        // Interactive token acquisition sequence routing through the WAM broker or fallback web UI
        public async Task<string> AcquireTokenInteractiveAsync(string[] scopes)
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

            ExtractAndApplyRouting(result);
            IsAuthenticated = true;
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

        public async Task SignOutAsync()
        {
            var accounts = await _pca.GetAccountsAsync();

            while (accounts.Any())
            {
                await _pca.RemoveAsync(accounts.First());
                accounts = await _pca.GetAccountsAsync();
            }

            IsAuthenticated = false;
            Log.Info("AuthService", "User signed out successfully and token cache cleared.");
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