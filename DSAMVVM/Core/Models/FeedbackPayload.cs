using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;

namespace DSAMVVM.Core.Models
{
    // Record to transport dynamic application state and user input to the Graph API service
    public record FeedbackPayload(
        string TeamId,
        string ChannelId,
        string FeedbackType,
        string Details,
        string CurrentView,
        string? RecentError = null,
        string? CurrentSupportTeam = null,
        string? RecentQuery = null,
        string? RecentDepartment = null
    ) : ITeamsMessagePayload
    {
        public static string AppVersion => Globals.g_AppVersion ?? "Unknown";

        // Routes the subject line formatting based on the injected FeedbackType
        public string GetSubject()
        {
            return FeedbackType switch
            {
                "Bug" => $"Bug Report - {AppVersion}",
                "Request" => $"Feature Request - {AppVersion}",
                "Note" => "Note Request",
                "Update" => "Update Request",
                _ => $"{FeedbackType}"
            };
        }

        // Constructs the HTML body payload for Teams based on the injected FeedbackType
        public string GetHtmlBody()
        {
            return FeedbackType switch
            {
                "Bug" => $"<p><strong>Issue:</strong> {Details}<br>" +
                         $"<strong>Current View:</strong> {CurrentView}<br>" +
                         $"<strong>Error:</strong> {(string.IsNullOrWhiteSpace(RecentError) ? "N/A" : RecentError)}</p>",

                "Request" => $"<p><strong>Request:</strong> {Details}<br>" +
                             $"<strong>Current version:</strong> {AppVersion}<br>" +
                             $"<strong>Current view:</strong> {CurrentView}</p>",

                "Note" => $"<p><strong>Current Support Team:</strong> {CurrentSupportTeam}</p>" +
                             $"<p><strong>Department Number:</strong> {RecentDepartment}</p>" +
                             $"<p><strong>Note Details:</strong> {Details}<br>" +
                             $"<strong>Example NetID:</strong> {RecentQuery}</p>",

                "Update" => $"<p><strong>Current Support Team:</strong> {CurrentSupportTeam}<br>" +
                            $"<strong>Expected Support Team:</strong> {Details}</p>" +
                            $"<p><strong>Department Number:</strong> {RecentDepartment}<br>" +
                            $"<strong>Example NetID:</strong> {RecentQuery}</p>",

                _ => $"<p><strong>Details:</strong> {Details}</p>"
            };
        }
    }
}