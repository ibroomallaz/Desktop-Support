using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace DSAMVVM.Core.Services.Graph
{
    /// <summary>
    /// Lightweight alternative for posting messages directly to the Microsoft Graph Teams API via raw HTTP.
    /// </summary>
    public class RawHttpTeamsService
    {
        private static readonly Lazy<HttpClient> FallbackClient = new(() => new HttpClient());

        /// <summary>
        /// Posts a raw text message directly to a Teams channel endpoint using a pre-fetched delegated access token.
        /// </summary>
        public static async Task<bool> PostMessageRawAsync(string accessToken, string teamId, string channelId, string messageText)
        {
            var requestUrl = $"https://graph.microsoft.com/v1.0/teams/{teamId}/channels/{channelId}/messages";

            // Construct raw JSON payload body
            var jsonPayload = $"{{\"body\": {{\"contentType\": \"text\", \"content\": \"{messageText}\"}}}}";

            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = content;

            // Inject the dynamic OAuth2 token into the per-request header collection
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var httpService = App.Services.GetService<IHttpService>();
                HttpResponseMessage response;
                if (httpService != null)
                {
                    response = await httpService.SendAsync(request);
                }
                else
                {
                    response = await FallbackClient.Value.SendAsync(request);
                }

                using (response)
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"// [HTTP ERROR]: Raw post network transaction failed. Message: {ex.Message}");
                return false;
            }
        }
    }
}
