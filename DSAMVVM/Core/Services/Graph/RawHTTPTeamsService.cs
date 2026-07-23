using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace DSAMVVM.Core.Services.Graph
{
    public class RawHttpTeamsService
    {
        // --- METHOD: RAW HTTP CHANNEL POST ---
        // Issues a direct JSON string payload via raw HttpClient using a pre-fetched delegated access token.
        public static async Task<bool> PostMessageRawAsync(string accessToken, string teamId, string channelId, string messageText)
        {
            using var client = new HttpClient();
            // Inject the dynamic OAuth2 token into the standard Authorization header request space
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestUrl = $"https://graph.microsoft.com/v1.0/teams/{teamId}/channels/{channelId}/messages";

            // Construct raw JSON payload body
            var jsonPayload = $"{{\"body\": {{\"contentType\": \"text\", \"content\": \"{messageText}\"}}}}";

            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            try
            {
                var response = await client.PostAsync(requestUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"// [HTTP ERROR]: Raw post network transaction failed. Message: {ex.Message}");
                return false;
            }
        }
    }
}