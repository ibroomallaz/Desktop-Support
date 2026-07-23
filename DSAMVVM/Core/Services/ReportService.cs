using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Services.Graph;

namespace DSAMVVM.Core.Services
{
    public class ReportService(
        TeamsRoutingService routingService,
        RawHttpTeamsService teamsService,
        IAuthenticationService authService) : IReportService
    {
        private readonly TeamsRoutingService _routingService = routingService;
        private readonly RawHttpTeamsService _teamsService = teamsService;
        private readonly IAuthenticationService _authService = authService;

        public async Task<bool> SubmitDataDiscrepancyAsync(string targetUser, string incorrectGroup, string reporter)
        {
            string teamId = _routingService.TeamId;
            string channelId = _routingService.GetChannelId("Update"); // Matches your Entra "Update" claim

            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(channelId)) return false;

            string message = $"🚨 **Data Discrepancy Reported**<br>" +
                             $"- **Target User:** {targetUser}<br>" +
                             $"- **Listed Group:** {incorrectGroup}<br>" +
                             $"- **Reported By:** {reporter}";

            return await SendToTeamsAsync(teamId, channelId, message);
        }

        public async Task<bool> SubmitBugReportAsync(string reporter, string appVersion, string userComments)
        {
            string teamId = _routingService.TeamId;
            string channelId = _routingService.GetChannelId("Bug"); // Matches your Entra "Bug" claim

            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(channelId)) return false;

            string message = $"🐛 **App Bug Reported**<br>" +
                             $"- **Reporter:** {reporter}<br>" +
                             $"- **Version:** {appVersion}<br>" +
                             $"- **Details:** {userComments}";

            return await SendToTeamsAsync(teamId, channelId, message);
        }

        public async Task<bool> SubmitFeatureRequestAsync(string reporter, string appVersion, string requestDetails)
        {
            string teamId = _routingService.TeamId;
            string channelId = _routingService.GetChannelId("Request"); // Matches your Entra "Request" claim

            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(channelId)) return false;

            string message = $"💡 **New Feature Request**<br>" +
                             $"- **Requested By:** {reporter}<br>" +
                             $"- **Version:** {appVersion}<br>" +
                             $"- **Idea:** {requestDetails}";

            return await SendToTeamsAsync(teamId, channelId, message);
        }

        // --- Centralized helper to handle tokens, posting, and error suppression ---
        private async Task<bool> SendToTeamsAsync(string teamId, string channelId, string message)
        {
            try
            {
                // Request token dynamically just before posting
                string token = await _authService.GetGraphAccessTokenAsync(["ChannelMessage.Send"]);
                return await RawHttpTeamsService.PostMessageRawAsync(token, teamId, channelId, message);
            }
            catch (Exception ex)
            {
                // Optional: Log the exception locally if you have a file logger
                System.Diagnostics.Debug.WriteLine($"[ReportService Error]: {ex.Message}");
                return false;
            }
        }
    }
}