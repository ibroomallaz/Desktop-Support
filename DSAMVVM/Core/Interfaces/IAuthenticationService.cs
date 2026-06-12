namespace DSAMVVM.Core.Interfaces
{
    public interface IAuthenticationService
    {
        // Silent token acquisition via WAM broker, falling back to dsa:// interactive flow
        Task<string> GetGraphAccessTokenAsync(string[] scopes);

        // Ingests deep link query arguments to catch interactive authorization code bounces
        void ProcessAuthRedirect(string url);
    }
}