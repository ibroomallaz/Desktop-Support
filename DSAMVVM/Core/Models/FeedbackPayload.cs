using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using System.Net;

namespace DSAMVVM.Core.Models
{
    // Encapsulates telemetry, diagnostic state, and dynamic user input for Microsoft Teams posting
    public sealed record FeedbackPayload(
        string TeamId,
        string ChannelId,
        string FeedbackType,
        string Details,
        string CurrentView,
        string? RecentError,
        string? CurrentSupportTeam = null,
        string? RecentQuery = null,
        string? RecentDepartment = null,
        string? PetName = null,
        string? PetSpecies = null,
        string? PetRole = null,
        string? PetOwner = null,
        string? PetBlurb = null,
        string? SubmitterNetId = null
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
                "ServiceMeow" => $"🐾 ServiceMeow Nomination: {PetName ?? "New Pet"}",
                _ => $"{FeedbackType}"
            };
        }

        // Constructs the HTML body payload for Teams based on the injected FeedbackType
        public string GetHtmlBody()
        {
            if (FeedbackType == "ServiceMeow")
            {
                string safePet = WebUtility.HtmlEncode(PetName?.Trim() ?? "Unnamed");
                string safeSpecies = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(PetSpecies) ? "Pet" : PetSpecies.Trim());
                string safeRole = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(PetRole) ? "Chief Morale Officer" : PetRole.Trim());

                // Owner / Handler is attributed directly to the signed-in poster's NetID (clean without @)
                string rawPoster = (SubmitterNetId ?? PetOwner ?? string.Empty).Trim().TrimStart('@');
                string posterNetId = rawPoster.Contains('@') ? rawPoster.Split('@')[0] : rawPoster;
                if (string.IsNullOrWhiteSpace(posterNetId)) posterNetId = "team";
                string ownerDisplay = WebUtility.HtmlEncode(posterNetId);

                string safeBlurb = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(PetBlurb) ? Details : PetBlurb.Trim());

                return $"""
                    <div style="font-family: 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #1E293B;">
                        <div style="font-size: 16px; font-weight: 700; color: #AB0520; margin-bottom: 8px;">
                            🐾 New ServiceMeow Nomination!
                        </div>
                        <table style="border-collapse: collapse; width: 100%; margin-bottom: 12px;">
                            <tr>
                                <td style="padding: 4px 8px 4px 0; font-weight: 600; width: 120px; color: #475569;">Pet Name:</td>
                                <td style="padding: 4px 0; color: #0F172A; font-weight: 600;">{safePet}</td>
                            </tr>
                            <tr>
                                <td style="padding: 4px 8px 4px 0; font-weight: 600; color: #475569;">Species / Type:</td>
                                <td style="padding: 4px 0; color: #0F172A;">{safeSpecies}</td>
                            </tr>
                            <tr>
                                <td style="padding: 4px 8px 4px 0; font-weight: 600; color: #475569;">Official Role:</td>
                                <td style="padding: 4px 0; color: #0F172A;">{safeRole}</td>
                            </tr>
                            <tr>
                                <td style="padding: 4px 8px 4px 0; font-weight: 600; color: #475569;">Owner / Handler:</td>
                                <td style="padding: 4px 0; color: #0F172A;"><strong>{ownerDisplay}</strong></td>
                            </tr>
                        </table>

                        <div style="margin: 10px 0; padding: 10px 14px; background-color: #F8FAFC; border-left: 4px solid #AB0520; font-style: italic; border-radius: 2px;">
                            "{safeBlurb}"
                        </div>

                        <div style="margin-top: 14px; padding: 6px 0; color: #475569; font-size: 13px;">
                            📸 <strong>Photo Submission:</strong> Please reply to this thread with one or more photos of <strong>{safePet}</strong> to finalize the mascot entry!
                        </div>
                    </div>
                    """;
            }

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
