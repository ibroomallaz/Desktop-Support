using DSAMVVM.Core.Logging;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSAMVVM.Core.Services.Graph
{
    public class TeamsRoutingService
    {
        private static readonly HttpClient HttpClient = new();
        private readonly Dictionary<string, string> _channels = new(StringComparer.OrdinalIgnoreCase);
        public string TeamId { get; private set; } = string.Empty;

        public void InitializeFromClaims(IEnumerable<Claim> claims)
        {
            var roleClaims = claims.Where(c => c.Type == "roles");
            string? boxToken = null;

            foreach (var roleClaim in roleClaims)
            {
                if (string.IsNullOrWhiteSpace(roleClaim.Value)) continue;

                string[] routePairs = roleClaim.Value.Split('|', StringSplitOptions.RemoveEmptyEntries);

                foreach (string pair in routePairs)
                {
                    string[] keyValue = pair.Split([':'], 2, StringSplitOptions.RemoveEmptyEntries);

                    if (keyValue.Length != 2) continue;
                    string key = keyValue[0].Trim();
                    string value = keyValue[1].Trim();

                    if (key.Equals("TeamID", StringComparison.OrdinalIgnoreCase))
                    {
                        TeamId = value;
                    }
                    else if (key.Equals("Box", StringComparison.OrdinalIgnoreCase))
                    {
                        boxToken = value;
                    }
                    else
                    {
                        _channels[key] = value;
                    }
                }
            }

            // If a Box configuration token is present in the claim, asynchronously fetch the dynamic routing JSON
            if (!string.IsNullOrWhiteSpace(boxToken))
            {
                _ = FetchRoutingFromBoxAsync(boxToken);
            }
        }

        public async Task FetchRoutingFromBoxAsync(string boxToken)
        {
            try
            {
                string url = $"https://arizona.box.com/shared/static/{boxToken}.json";
                Log.Info("TeamsRouting", "Fetching dynamic Teams routing configuration from remote storage.");

                string json = await HttpClient.GetStringAsync(url).ConfigureAwait(false);
                var config = JsonSerializer.Deserialize<TeamsRoutingConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    if (!string.IsNullOrWhiteSpace(config.TeamId))
                    {
                        TeamId = config.TeamId.Trim();
                    }

                    if (config.Channels != null)
                    {
                        foreach (var (chName, chId) in config.Channels)
                        {
                            if (!string.IsNullOrWhiteSpace(chName) && !string.IsNullOrWhiteSpace(chId))
                            {
                                _channels[chName.Trim()] = chId.Trim();
                            }
                        }
                    }

                    Log.Info("TeamsRouting", $"Successfully loaded {config.Channels?.Count ?? 0} Teams channels from remote configuration.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("TeamsRouting", $"Failed to fetch dynamic routing configuration from remote storage: {ex.Message}");
            }
        }

        public string GetChannelId(string channelKey)
        {
            if (_channels.TryGetValue(channelKey, out string? id) && !string.IsNullOrWhiteSpace(id))
            {
                return id;
            }

            return string.Empty;
        }

        private sealed class TeamsRoutingConfig
        {
            [JsonPropertyName("teamId")]
            public string? TeamId { get; init; }

            [JsonPropertyName("channels")]
            public Dictionary<string, string>? Channels { get; init; }
        }
    }
}