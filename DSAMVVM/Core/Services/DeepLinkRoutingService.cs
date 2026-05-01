using System.Text;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Logging;

namespace DSAMVVM.Core.Services
{
    public class DeepLinkRoutingService : IDeepLinkRoutingService
    {
        private readonly IADService _adService;
        private readonly IDepartmentService _deptService;

        public DeepLinkRoutingService(IADService adService, IDepartmentService deptService)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
        }

        public async Task<string> HandleLinkAsync(string url, string? contextNetId = null)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            Log.Info("LinkRouter", $"User clicked deep link: '{url}'");

            try
            {
                if (url.StartsWith("dsa://team/", StringComparison.OrdinalIgnoreCase))
                {
                    var teamName = Uri.UnescapeDataString(url["dsa://team/".Length..]);
                    return await OrganizationalRenderer.RenderTeamInfoAsync(teamName, _deptService);
                }

                if (url.StartsWith("dsa://license/adobe/", StringComparison.OrdinalIgnoreCase))
                {
                    var targetNetId = url["dsa://license/adobe/".Length..];
                    Log.Debug("LinkRouter", $"Executing Adobe license check for '{targetNetId}'");

                    var status = await _adService.CheckAdobeLicensesAsync(targetNetId);
                    return IdentityRenderer.RenderAdobeLicenseStatus(targetNetId, status.HasAcrobatPro, status.HasCreativeCloud);
                }

                if (url.StartsWith("dsa://license/o365/", StringComparison.OrdinalIgnoreCase))
                {
                    var segments = url["dsa://license/o365/".Length..].Split('/');
                    if (segments.Length >= 2)
                    {
                        var netid = segments[0];
                        var base64Data = segments[1];
                        var rawLicense = Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
                        return IdentityRenderer.RenderRawLicenseInfo(netid, rawLicense);
                    }
                }

                Log.Warn("LinkRouter", $"Unrecognized deep link format: '{url}'");
                return string.Empty;
            }
            catch (Exception ex)
            {
                // Catch any network or AD failures triggered by clicking a link
                Log.Error("LinkRouter", $"Failed to process deep link '{url}'", ex);

                var errDoc = new FlowDocMarkupBuilder();
                errDoc.AddError($"Error processing link: {ex.Message}");
                return errDoc.ToString();
            }
        }
    }
}