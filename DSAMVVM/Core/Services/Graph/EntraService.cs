using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model.Entra;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions.Authentication;

namespace DSAMVVM.Core.Services.Graph
{
    /// <summary>
    /// Provides Microsoft Graph queries for Entra ID devices, owners/users, and group memberships.
    /// </summary>
    public class EntraService(IAuthenticationService authService) : IEntraService
    {
        public static readonly string[] RequiredDeviceScopes =
        [
            "Device.Read.All",
            "User.Read.All",
            "GroupMember.Read.All"
        ];

        private static readonly string[] DeviceSelectFields =
        [
            "id",
            "deviceId",
            "displayName",
            "operatingSystem",
            "operatingSystemVersion",
            "manufacturer",
            "model",
            "mdmAppId",
            "isManaged",
            "managementType",
            "enrollmentType",
            "deviceOwnership",
            "profileType",
            "trustType",
            "onPremisesSyncEnabled",
            "onPremisesLastSyncDateTime",
            "approximateLastSignInDateTime",
            "accountEnabled",
            "registrationDateTime"
        ];

        private readonly IAuthenticationService _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        private GraphServiceClient CreateGraphClient()
        {
            var tokenProvider = new InlineTokenProvider(_authService, RequiredDeviceScopes);
            var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
            return new GraphServiceClient(authProvider);
        }

        public async Task<EntraDeviceSearchResult> SearchDevicesAsync(string query, CancellationToken cancellationToken = default)
        {
            var trimmed = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return new EntraDeviceSearchResult
                {
                    Query = string.Empty,
                    Exists = false,
                    ErrorMessage = "Device search query cannot be empty."
                };
            }

            try
            {
                var graphClient = CreateGraphClient();

                string filter = Guid.TryParse(trimmed, out var parsedGuid)
                    ? $"id eq '{parsedGuid}' or deviceId eq '{parsedGuid}'"
                    : $"startswith(displayName,'{EscapeODataString(trimmed)}')";

                var response = await graphClient.Devices.GetAsync(config =>
                {
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Select = DeviceSelectFields;
                    config.QueryParameters.Top = 25;
                }, cancellationToken).ConfigureAwait(false);

                var devices = response?.Value ?? [];
                if (devices.Count == 0)
                {
                    return new EntraDeviceSearchResult
                    {
                        Query = trimmed,
                        Exists = false,
                        ErrorMessage = $"No Entra device found matching '{trimmed}'."
                    };
                }

                var enrichmentTasks = devices.Select(device => BuildDeviceInfoAsync(graphClient, device, cancellationToken));
                var enriched = await Task.WhenAll(enrichmentTasks).ConfigureAwait(false);

                var sorted = enriched
                    .OrderByDescending(d => string.Equals(d.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(d => d.Groups.Count > 0)
                    .ThenByDescending(d => d.Groups.Count)
                    .ThenByDescending(d => d.ApproximateLastSignInDateTime ?? DateTimeOffset.MinValue)
                    .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new EntraDeviceSearchResult
                {
                    Query = trimmed,
                    Exists = true,
                    Devices = sorted
                };
            }
            catch (ODataError ex)
            {
                string message = FormatODataError("Graph device query failed", ex);
                Log.Error("EntraService", message, ex);
                return new EntraDeviceSearchResult
                {
                    Query = trimmed,
                    Exists = false,
                    ErrorMessage = message
                };
            }
            catch (Exception ex)
            {
                Log.Error("EntraService", $"Entra device lookup failed for '{trimmed}'", ex);
                return new EntraDeviceSearchResult
                {
                    Query = trimmed,
                    Exists = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static async Task<EntraDeviceInfo> BuildDeviceInfoAsync(
            GraphServiceClient graphClient,
            Device device,
            CancellationToken cancellationToken)
        {
            string objectId = device.Id ?? string.Empty;

            var info = new EntraDeviceInfo
            {
                ObjectId = objectId,
                DeviceId = device.DeviceId ?? string.Empty,
                DisplayName = device.DisplayName ?? string.Empty,
                OperatingSystem = device.OperatingSystem,
                OperatingSystemVersion = device.OperatingSystemVersion,
                Manufacturer = device.Manufacturer,
                Model = device.Model,
                MdmAppId = device.MdmAppId,
                MdmDisplayName = ResolveMdmDisplayName(device.MdmAppId, device.IsManaged, device.ManagementType),
                IsManaged = device.IsManaged,
                ManagementType = device.ManagementType,
                EnrollmentType = device.EnrollmentType,
                DeviceOwnership = device.DeviceOwnership,
                ProfileType = device.ProfileType,
                TrustType = device.TrustType,
                JoinTypeDisplay = FormatJoinType(device.TrustType),
                OnPremisesSyncEnabled = device.OnPremisesSyncEnabled,
                OnPremisesLastSyncDateTime = device.OnPremisesLastSyncDateTime,
                ApproximateLastSignInDateTime = device.ApproximateLastSignInDateTime,
                AccountEnabled = device.AccountEnabled,
                RegistrationDateTime = device.RegistrationDateTime
            };

            if (string.IsNullOrWhiteSpace(objectId))
            {
                return info;
            }

            var ownersTask = FetchOwnersAsync(graphClient, objectId, cancellationToken);
            var groupsTask = FetchGroupsAsync(graphClient, objectId, cancellationToken);

            await Task.WhenAll(ownersTask, groupsTask).ConfigureAwait(false);

            var (owners, ownerError) = ownersTask.Result;
            var (groups, groupError) = groupsTask.Result;

            info.Owners.AddRange(owners);
            info.OwnerLookupError = ownerError;
            info.Groups.AddRange(groups);
            info.GroupLookupError = groupError;

            return info;
        }

        private static async Task<(List<EntraDeviceOwnerInfo> Owners, string? Error)> FetchOwnersAsync(
            GraphServiceClient graphClient,
            string deviceObjectId,
            CancellationToken cancellationToken)
        {
            try
            {
                var owners = new List<EntraDeviceOwnerInfo>();
                var page = await graphClient.Devices[deviceObjectId].RegisteredOwners.GraphUser.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail"];
                }, cancellationToken).ConfigureAwait(false);

                if (page != null)
                {
                    var iterator = PageIterator<User, UserCollectionResponse>.CreatePageIterator(
                        graphClient,
                        page,
                        user =>
                        {
                            owners.Add(MapUser(user));
                            return true;
                        });

                    await iterator.IterateAsync(cancellationToken).ConfigureAwait(false);
                }

                return (owners, null);
            }
            catch (ODataError ex)
            {
                string err = FormatODataError("Owner lookup failed", ex);
                Log.Warn("EntraService", $"{deviceObjectId}: {err}");
                return ([], err);
            }
            catch (Exception ex)
            {
                Log.Warn("EntraService", $"{deviceObjectId}: Owner lookup failed - {ex.Message}");
                return ([], ex.Message);
            }
        }

        private static EntraDeviceOwnerInfo MapUser(User user) => new()
        {
            ObjectId = user.Id ?? string.Empty,
            DisplayName = user.DisplayName ?? string.Empty,
            UserPrincipalName = user.UserPrincipalName,
            Mail = user.Mail
        };

        private static async Task<(List<EntraDeviceGroupInfo> Groups, string? Error)> FetchGroupsAsync(
            GraphServiceClient graphClient,
            string deviceObjectId,
            CancellationToken cancellationToken)
        {
            try
            {
                var directIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var directPage = await graphClient.Devices[deviceObjectId].MemberOf.GraphGroup.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id"];
                    config.QueryParameters.Top = 999;
                }, cancellationToken).ConfigureAwait(false);

                if (directPage != null)
                {
                    var directIterator = PageIterator<Group, GroupCollectionResponse>.CreatePageIterator(
                        graphClient,
                        directPage,
                        g =>
                        {
                            if (!string.IsNullOrWhiteSpace(g.Id))
                            {
                                directIds.Add(g.Id);
                            }
                            return true;
                        });

                    await directIterator.IterateAsync(cancellationToken).ConfigureAwait(false);
                }

                var groups = new List<EntraDeviceGroupInfo>();
                var transitivePage = await graphClient.Devices[deviceObjectId].TransitiveMemberOf.GraphGroup.GetAsync(config =>
                {
                    config.QueryParameters.Select =
                    [
                        "id",
                        "displayName",
                        "description",
                        "groupTypes",
                        "onPremisesSyncEnabled",
                        "securityEnabled",
                        "mailEnabled"
                    ];
                    config.QueryParameters.Top = 999;
                }, cancellationToken).ConfigureAwait(false);

                if (transitivePage != null)
                {
                    var transitiveIterator = PageIterator<Group, GroupCollectionResponse>.CreatePageIterator(
                        graphClient,
                        transitivePage,
                        g =>
                        {
                            string id = g.Id ?? string.Empty;
                            bool isDynamic = g.GroupTypes?.Any(t => string.Equals(t, "DynamicMembership", StringComparison.OrdinalIgnoreCase)) == true;
                            bool isM365 = g.GroupTypes?.Any(t => string.Equals(t, "Unified", StringComparison.OrdinalIgnoreCase)) == true;

                            groups.Add(new EntraDeviceGroupInfo
                            {
                                ObjectId = id,
                                DisplayName = g.DisplayName ?? "(Unnamed Group)",
                                Description = g.Description,
                                IsDirect = directIds.Contains(id),
                                IsDynamic = isDynamic,
                                IsSyncedFromOnPrem = g.OnPremisesSyncEnabled == true,
                                SecurityEnabled = g.SecurityEnabled,
                                MailEnabled = g.MailEnabled,
                                GroupTypeLabel = ResolveGroupTypeLabel(isM365, g.SecurityEnabled, g.MailEnabled)
                            });
                            return true;
                        });

                    await transitiveIterator.IterateAsync(cancellationToken).ConfigureAwait(false);
                }

                groups.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
                return (groups, null);
            }
            catch (ODataError ex)
            {
                string err = FormatODataError("Group lookup failed", ex);
                Log.Warn("EntraService", $"{deviceObjectId}: {err}");
                return ([], err);
            }
            catch (Exception ex)
            {
                Log.Warn("EntraService", $"{deviceObjectId}: Group lookup failed - {ex.Message}");
                return ([], ex.Message);
            }
        }

        private static string ResolveGroupTypeLabel(bool isM365, bool? securityEnabled, bool? mailEnabled)
        {
            if (isM365) return "M365";
            if (securityEnabled == true && mailEnabled == true) return "Mail-Enabled Security";
            if (securityEnabled == true) return "Security";
            if (mailEnabled == true) return "Distribution";
            return "Group";
        }

        private static string EscapeODataString(string input) => input.Replace("'", "''");

        private static string FormatODataError(string prefix, ODataError ex)
        {
            string code = ex.Error?.Code ?? "ODataError";
            string msg = ex.Error?.Message ?? ex.Message;
            return $"{prefix}: [{code}] {msg}";
        }

        private static string FormatJoinType(string? trustType) => trustType switch
        {
            "ServerAd" => "Hybrid Entra Joined",
            "AzureAd" => "Entra Joined",
            "Workplace" => "Entra Registered",
            null or "" => "Unknown",
            _ => trustType
        };

        private static string ResolveMdmDisplayName(string? mdmAppId, bool? isManaged, string? managementType)
        {
            string appLabel = mdmAppId switch
            {
                "0000000a-0000-0000-c000-000000000000" => "Microsoft Intune",
                "00000003-0000-0ff1-ce00-000000000000" => "Microsoft Exchange / O365 Mobile",
                null or "" => isManaged == true ? "Managed (No MDM App ID)" : "None",
                _ => mdmAppId
            };

            if (!string.IsNullOrWhiteSpace(managementType))
            {
                return $"{appLabel} ({managementType})";
            }

            return appLabel;
        }
    }
}
