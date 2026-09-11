using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensibility;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DSAMVVM.Core.Services.Graph
{
    public partial class AuthenticationService : IAuthenticationService
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

            var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
            {
                Title = "Desktop Support App"
            };
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
        public async Task<string> AcquireTokenInteractiveAsync(string[] scopes, IntPtr? parentWindowHandle = null)
        {
            IntPtr windowHandle = parentWindowHandle ?? IntPtr.Zero;
            Window? activeWindow = null;
            var untoppedWindows = new List<Window>();

            // Extracts the native window handle from the active/foreground window on the WPF UI thread
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (windowHandle == IntPtr.Zero)
                    {
                        activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
                                       ?? Application.Current.Windows.OfType<Window>().LastOrDefault(w => w.IsVisible)
                                       ?? Application.Current.MainWindow;

                        if (activeWindow != null)
                        {
                            if (activeWindow.WindowState == WindowState.Minimized)
                            {
                                activeWindow.WindowState = WindowState.Normal;
                            }

                            activeWindow.Activate();
                            activeWindow.Focus();

                            windowHandle = new WindowInteropHelper(activeWindow).EnsureHandle();
                        }
                    }
                    else
                    {
                        activeWindow = Application.Current.Windows.OfType<Window>()
                            .FirstOrDefault(w => new WindowInteropHelper(w).Handle == windowHandle);
                    }

                    if (windowHandle != IntPtr.Zero)
                    {
                        SwitchToThisWindow(windowHandle, true);
                        SetForegroundWindow(windowHandle);
                    }

                    // Any window that has Topmost = true (such as FeedbackWindow) will occlude
                    // the external MSAL WAM broker or browser prompt. Temporarily disable Topmost.
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window.IsVisible && window.Topmost)
                        {
                            window.Topmost = false;
                            untoppedWindows.Add(window);
                        }
                    }
                });
            }

            // Start a lightweight background watcher to bring the MSAL/WAM popup dialog to the front
            // as soon as it is spawned and attached to the parent window handle.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (windowHandle != IntPtr.Zero)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(100, cts.Token);
                            IntPtr popup = GetWindow(windowHandle, GW_ENABLEDPOPUP);
                            if (popup != IntPtr.Zero && popup != windowHandle)
                            {
                                SetWindowPos(popup, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                                BringWindowToTop(popup);
                                SetForegroundWindow(popup);
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Log.Debug("AuthService", $"Popup watcher ignored exception: {ex.Message}");
                    }
                }, cts.Token);
            }

            try
            {
                // Binds the interactive MSAL security dialog to the parent window process
                var interactiveBuilder = _pca.AcquireTokenInteractive(scopes)
                    .WithCustomWebUi(new DeepLinkWebUi(this));

                if (windowHandle != IntPtr.Zero)
                {
                    interactiveBuilder = interactiveBuilder.WithParentActivityOrWindow(windowHandle);
                }

                var result = await interactiveBuilder.ExecuteAsync();

                ExtractAndApplyRouting(result);
                IsAuthenticated = true;
                return result.AccessToken;
            }
            finally
            {
                cts.Cancel();

                // Restore Topmost on any windows that were originally Topmost, and re-activate the parent window
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var window in untoppedWindows)
                        {
                            window.Topmost = true;
                        }

                        if (activeWindow != null && activeWindow.IsVisible)
                        {
                            activeWindow.Activate();
                            activeWindow.Focus();
                            if (windowHandle != IntPtr.Zero)
                            {
                                SetForegroundWindow(windowHandle);
                            }
                        }
                    });
                }
            }
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

        #region Win32 P/Invoke Declarations

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll", EntryPoint = "SwitchToThisWindow")]
        private static partial void SwitchToThisWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAltTab);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool BringWindowToTop(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [LibraryImport("user32.dll")]
        private static partial IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint GW_ENABLEDPOPUP = 6;

        #endregion
    }
}
