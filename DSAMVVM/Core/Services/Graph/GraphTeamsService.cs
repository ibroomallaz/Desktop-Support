using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace DSAMVVM.Core.Services.Graph
{
    // Token bridge implementation for Microsoft Graph authentication.
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
        // Executes a diagnostic post to verify Entra Token routing GUIDs.
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
                Log.Error("GraphTeamsService", errorDetails, ex);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("GraphTeamsService", "STANDARD ERROR", ex);
                return false;
            }
        }

        // Transmits a formatted message payload to a specified Teams channel.
        public static async Task<bool> PostMessageAsync(GraphServiceClient graphClient, ITeamsMessagePayload payload)
        {
            try
            {
                var chatMessage = new Microsoft.Graph.Models.ChatMessage
                {
                    Subject = payload.GetSubject(),
                    Body = new Microsoft.Graph.Models.ItemBody
                    {
                        ContentType = Microsoft.Graph.Models.BodyType.Html,
                        Content = payload.GetHtmlBody()
                    }
                };

                await graphClient.Teams[payload.TeamId].Channels[payload.ChannelId].Messages.PostAsync(chatMessage);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("GraphTeamsService", $"TEAMS POST ERROR: Failed to post message to channel {payload.ChannelId}.", ex);
                return false;
            }
        }
    }
}