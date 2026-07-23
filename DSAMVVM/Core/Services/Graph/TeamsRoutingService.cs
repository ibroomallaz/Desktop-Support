using System.Security.Claims;

namespace DSAMVVM.Core.Services.Graph
{
    public class TeamsRoutingService
    {
        private readonly Dictionary<string, string> _channels = [];
        public string TeamId { get; private set; } = string.Empty;

        public void InitializeFromClaims(IEnumerable<Claim> claims)
        {
            var roleClaim = claims.FirstOrDefault(c => c.Type == "roles" && c.Value.Contains("TeamID:"));

            if (roleClaim != null)
            {
                string[] routePairs = roleClaim.Value.Split('|', StringSplitOptions.RemoveEmptyEntries);

                foreach (string pair in routePairs)
                {
                    string[] keyValue = pair.Split([':'], 2, StringSplitOptions.RemoveEmptyEntries);

                    if (keyValue.Length == 2)
                    {
                        string key = keyValue[0].Trim();
                        string value = keyValue[1].Trim();

                        if (key.Equals("TeamID", StringComparison.OrdinalIgnoreCase))
                        {
                            TeamId = value;
                        }
                        else
                        {
                            _channels[key] = value;
                        }
                    }
                }
            }
        }

        public string GetChannelId(string channelKey)
        {
            return _channels.TryGetValue(channelKey, out string? id) ? id : string.Empty;
        }
    }
}