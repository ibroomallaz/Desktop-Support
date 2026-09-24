namespace DSAMVVM.Core.Interfaces
{
    public interface IAuthenticationService
    {
        // Event triggered when the authentication state changes
        event Action<bool> AuthenticationStateChanged;

        // Synchronous state for UI data binding and immediate conditional checks
        bool IsAuthenticated { get; }

        // Current user principal name (UPN) if authenticated or detected from broker cache
        string? CurrentAccountUpn { get; }

        // Asynchronous verification of silent token acquisition prior to executing Graph requests
        Task<bool> ValidateAuthenticationAsync(string[] scopes);

        // Silent token cache verification with default scopes (e.g. User.Read)
        Task<bool> CheckCachedSignInAsync();

        // Silent token acquisition via WAM broker, falling back to dsa:// interactive flow
        Task<string> GetGraphAccessTokenAsync(string[] scopes);

        // Explicitly triggers the interactive authentication flow
        Task<string> AcquireTokenInteractiveAsync(string[] scopes, IntPtr? parentWindowHandle = null);

        // Ingests deep link query arguments to catch interactive authorization code bounces
        void ProcessAuthRedirect(string url);

        // Forced Sign-out and cache clear
        Task SignOutAsync();
    }
}
