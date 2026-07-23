using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace DSAMVVM.Core.Services.Graph
{
    // --- KIOTA TOKEN BRIDGE ---

    public class InlineTokenProvider(AuthenticationService authService, string[] scopes) : IAccessTokenProvider
    {
        public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();

        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            return await authService.GetGraphAccessTokenAsync(scopes);
        }
    }

    public class GraphTeamsService
    {
        // --- METHOD: TEST CHANNEL POSTING END-TO-END ---
        public static async Task<bool> RunDiagnosticPostTestAsync(GraphServiceClient graphClient, string teamId, string channelId)
        {
            try
            {
                var chatMessage = new Microsoft.Graph.Models.ChatMessage
                {
                    Body = new Microsoft.Graph.Models.ItemBody
                    {
                        Content = "🧪 **Automated Diagnostic Test:** Routing GUIDs successfully extracted from Entra Token!"
                    }
                };

                await graphClient.Teams[teamId].Channels[channelId].Messages.PostAsync(chatMessage);
                return true;
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
            {
                string errorDetails = $"GRAPH API REJECTION: {ex.Error?.Code} - {ex.Error?.Message}";
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine(errorDetails);
                System.Diagnostics.Debug.WriteLine("========================================");

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"STANDARD ERROR: {ex.Message}");
                return false;
            }
        }
    }
}