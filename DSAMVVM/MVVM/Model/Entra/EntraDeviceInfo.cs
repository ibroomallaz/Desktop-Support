namespace DSAMVVM.MVVM.Model.Entra
{
    public class EntraDeviceSearchResult
    {
        public string Query { get; init; } = string.Empty;
        public bool Exists { get; set; }
        public List<EntraDeviceInfo> Devices { get; init; } = [];
        public string? ErrorMessage { get; set; }
    }

    public class EntraDeviceInfo
    {
        public string ObjectId { get; init; } = string.Empty;
        public string DeviceId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        public string? OperatingSystem { get; init; }
        public string? OperatingSystemVersion { get; init; }
        public string? Manufacturer { get; init; }
        public string? Model { get; init; }

        public string? MdmAppId { get; init; }
        public string MdmDisplayName { get; init; } = string.Empty;
        public bool? IsManaged { get; init; }
        public string? ManagementType { get; init; }
        public string? EnrollmentType { get; init; }
        public string? DeviceOwnership { get; init; }
        public string? ProfileType { get; init; }

        public string? TrustType { get; init; }
        public string JoinTypeDisplay { get; init; } = string.Empty;

        public bool? OnPremisesSyncEnabled { get; init; }
        public DateTimeOffset? OnPremisesLastSyncDateTime { get; init; }

        public DateTimeOffset? ApproximateLastSignInDateTime { get; init; }
        public bool? AccountEnabled { get; init; }
        public DateTimeOffset? RegistrationDateTime { get; init; }

        public List<EntraDeviceOwnerInfo> Owners { get; init; } = [];
        public List<EntraDeviceGroupInfo> Groups { get; init; } = [];

        public string? OwnerLookupError { get; set; }
        public string? GroupLookupError { get; set; }
    }

    public class EntraDeviceOwnerInfo
    {
        public string ObjectId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? UserPrincipalName { get; init; }
        public string? Mail { get; init; }
    }

    public class EntraDeviceGroupInfo
    {
        public string ObjectId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsDirect { get; init; }
        public bool IsDynamic { get; init; }
        public bool IsSyncedFromOnPrem { get; init; }
        public bool? SecurityEnabled { get; init; }
        public bool? MailEnabled { get; init; }
        public string GroupTypeLabel { get; init; } = "Security";
    }
}
