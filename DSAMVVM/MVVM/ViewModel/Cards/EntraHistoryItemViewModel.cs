using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DSAMVVM.Core.Formatters;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.Entra;

namespace DSAMVVM.MVVM.ViewModel.Cards
{
    public class EntraDeviceRecordOption : ObservableObject
    {
        public EntraDeviceInfo Device { get; }
        public string Label { get; }
        public string SubLabel { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public EntraDeviceRecordOption(EntraDeviceInfo device, bool isSelected)
        {
            Device = device;
            Label = !string.IsNullOrWhiteSpace(device.DisplayName) ? device.DisplayName : device.ObjectId;
            string activity = device.ApproximateLastSignInDateTime.HasValue
                ? device.ApproximateLastSignInDateTime.Value.ToLocalTime().ToString("MMM dd, yyyy")
                : "No sign-in";
            string groupTag = $"{device.Groups.Count} {(device.Groups.Count == 1 ? "group" : "groups")}";
            string enabledTag = device.AccountEnabled == false ? " • Disabled" : string.Empty;
            SubLabel = $"{device.JoinTypeDisplay} • {groupTag} • {activity}{enabledTag}";
            _isSelected = isSelected;
        }
    }

    public class EntraHistoryItemViewModel : ObservableObject
    {
        private readonly IEntraService _entraService;
        private readonly IDeepLinkRoutingService _linkRouter;

        public event Action<EntraHistoryItemViewModel>? RemoveRequested;

        public string Query { get; }
        public DateTime Timestamp { get; private set; }
        public string TimeFormatted => Timestamp.ToString("h:mm tt");

        public string RelativeAgeText
        {
            get
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalMinutes < 1) return "just now";
                return diff.TotalMinutes < 60 ? $"{(int)diff.TotalMinutes} min ago" : $"{(int)diff.TotalHours}h ago";
            }
        }

        public bool IsStale => (DateTime.Now - Timestamp).TotalMinutes >= 5;

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStale));
                OnPropertyChanged(nameof(RelativeAgeText));
            }
        }

        // Result Set (Entra can return multiple device objects for the same hostname)
        public ObservableCollection<EntraDeviceInfo> Devices { get; } = [];
        public ObservableCollection<EntraDeviceRecordOption> DeviceOptions { get; } = [];
        public bool HasMultipleDevices => Devices.Count > 1;
        public int MatchedDeviceCount => Devices.Count;

        private bool _isDeviceSwitcherExpanded;
        public bool IsDeviceSwitcherExpanded
        {
            get => _isDeviceSwitcherExpanded;
            set => Set(ref _isDeviceSwitcherExpanded, value);
        }

        private bool _isFound;
        public bool IsFound
        {
            get => _isFound;
            private set { _isFound = value; OnPropertyChanged(); }
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set { _errorMessage = value; OnPropertyChanged(); }
        }

        private EntraDeviceInfo? _selectedDevice;
        public EntraDeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (Equals(_selectedDevice, value)) return;
                _selectedDevice = value;
                SyncActiveDeviceState();
            }
        }

        // Selected Device Display Properties
        public string DeviceName => !string.IsNullOrWhiteSpace(SelectedDevice?.DisplayName) ? SelectedDevice.DisplayName : Query;
        public string ObjectId => SelectedDevice?.ObjectId ?? string.Empty;
        public string DeviceId => SelectedDevice?.DeviceId ?? string.Empty;
        public string OperatingSystem => !string.IsNullOrWhiteSpace(SelectedDevice?.OperatingSystem) ? SelectedDevice.OperatingSystem : "Unknown OS";
        public string OperatingSystemVersion => SelectedDevice?.OperatingSystemVersion ?? string.Empty;
        public string OperatingSystemDisplay => string.IsNullOrWhiteSpace(OperatingSystemVersion)
            ? OperatingSystem
            : $"{OperatingSystem} ({OperatingSystemVersion})";

        public string HardwareDisplay
        {
            get
            {
                if (SelectedDevice == null) return string.Empty;
                var parts = new[] { SelectedDevice.Manufacturer, SelectedDevice.Model }
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                return string.Join(" ", parts);
            }
        }
        public bool HasHardware => !string.IsNullOrWhiteSpace(HardwareDisplay);

        public string MdmDisplayName => !string.IsNullOrWhiteSpace(SelectedDevice?.MdmDisplayName) ? SelectedDevice.MdmDisplayName : "None";
        public string MdmAppId => SelectedDevice?.MdmAppId ?? string.Empty;
        public bool HasMdmAppId => !string.IsNullOrWhiteSpace(MdmAppId);
        public bool IsManaged => SelectedDevice?.IsManaged == true;
        public string DeviceOwnership => SelectedDevice?.DeviceOwnership ?? string.Empty;
        public string EnrollmentType => SelectedDevice?.EnrollmentType ?? string.Empty;
        public bool HasOwnership => !string.IsNullOrWhiteSpace(DeviceOwnership);
        public bool HasEnrollmentType => !string.IsNullOrWhiteSpace(EnrollmentType);

        public string JoinTypeDisplay => !string.IsNullOrWhiteSpace(SelectedDevice?.JoinTypeDisplay) ? SelectedDevice.JoinTypeDisplay : "Unknown";
        public bool IsHybridJoined => string.Equals(SelectedDevice?.TrustType, "ServerAd", StringComparison.OrdinalIgnoreCase);
        public bool IsEntraJoined => string.Equals(SelectedDevice?.TrustType, "AzureAd", StringComparison.OrdinalIgnoreCase);
        public bool IsEntraRegistered => string.Equals(SelectedDevice?.TrustType, "Workplace", StringComparison.OrdinalIgnoreCase);

        public string OnPremSyncDisplay => SelectedDevice?.OnPremisesSyncEnabled switch
        {
            true when SelectedDevice.OnPremisesLastSyncDateTime.HasValue =>
                $"Synced ({FormatTimestamp(SelectedDevice.OnPremisesLastSyncDateTime)})",
            true => "Synced from On-Prem AD",
            false => "Cloud-Only",
            null => "Cloud-Only / Unknown"
        };

        public string LastActivityDisplay => FormatTimestamp(SelectedDevice?.ApproximateLastSignInDateTime);
        public string RegistrationDateDisplay => FormatTimestamp(SelectedDevice?.RegistrationDateTime);

        public bool IsEnabled => IsFound && SelectedDevice?.AccountEnabled != false;
        public bool IsDisabled => IsFound && SelectedDevice?.AccountEnabled == false;

        // Owners
        public ObservableCollection<EntraDeviceOwnerInfo> Owners { get; } = [];
        public bool HasOwners => Owners.Count > 0;
        public string? OwnerLookupError => SelectedDevice?.OwnerLookupError;
        public bool HasOwnerLookupError => !string.IsNullOrWhiteSpace(OwnerLookupError);

        // Division (derived from Entra group e.g. CDIV-Wrkst-Intune-All)
        private string _divisionCode = string.Empty;
        public string DivisionCode
        {
            get => _divisionCode;
            private set => Set(ref _divisionCode, value);
        }

        private string _divisionSourceGroup = string.Empty;
        public string DivisionSourceGroup
        {
            get => _divisionSourceGroup;
            private set => Set(ref _divisionSourceGroup, value);
        }

        public bool HasDivision => !string.IsNullOrWhiteSpace(DivisionCode);

        // Groups + Filter State
        public List<EntraDeviceGroupInfo> AllGroups { get; } = [];
        public ObservableCollection<EntraDeviceGroupInfo> FilteredGroups { get; } = [];
        public int TotalGroupCount => AllGroups.Count;
        public int DirectGroupCount => AllGroups.Count(g => g.IsDirect);
        public int FilteredGroupCount => FilteredGroups.Count;
        public bool HasGroups => AllGroups.Count > 0;
        public bool HasFilteredGroups => FilteredGroups.Count > 0;
        public string? GroupLookupError => SelectedDevice?.GroupLookupError;
        public bool HasGroupLookupError => !string.IsNullOrWhiteSpace(GroupLookupError);

        private bool _isGroupsExpanded;
        public bool IsGroupsExpanded
        {
            get => _isGroupsExpanded;
            set => Set(ref _isGroupsExpanded, value);
        }

        private bool _isGroupFilterOpen;
        public bool IsGroupFilterOpen
        {
            get => _isGroupFilterOpen;
            set { _isGroupFilterOpen = value; OnPropertyChanged(); }
        }

        private string _groupFilter = string.Empty;
        public string GroupFilter
        {
            get => _groupFilter;
            set
            {
                if (!Set(ref _groupFilter, value)) return;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    IsGroupsExpanded = true;
                    IsGroupFilterOpen = true;
                }
                ApplyGroupFilter();
            }
        }

        private bool _showDirectOnly;
        public bool ShowDirectOnly
        {
            get => _showDirectOnly;
            set
            {
                if (!Set(ref _showDirectOnly, value)) return;
                OnPropertyChanged(nameof(DirectFilterButtonText));
                ApplyGroupFilter();
            }
        }
        public string DirectFilterButtonText => ShowDirectOnly ? "Direct Only" : "All Memberships";

        public string CollapsedSummaryText
        {
            get
            {
                if (!IsFound) return "Not Found";
                string ownerPart = Owners.Count > 0
                    ? Owners[0].DisplayName
                    : "No Owner";
                return HasMultipleDevices
                    ? $"{MatchedDeviceCount} records • {ownerPart} • {TotalGroupCount} groups"
                    : $"{ownerPart} • {TotalGroupCount} groups";
            }
        }

        // Copy / Refresh State
        private bool _isCopied;
        public bool IsCopied { get => _isCopied; private set { _isCopied = value; OnPropertyChanged(); } }

        private bool _isSummaryCopied;
        public bool IsSummaryCopied { get => _isSummaryCopied; private set { _isSummaryCopied = value; OnPropertyChanged(); } }

        private bool _isRefreshing;
        public bool IsRefreshing { get => _isRefreshing; private set { _isRefreshing = value; OnPropertyChanged(); } }

        // Commands
        public ICommand ToggleExpandCommand { get; }
        public ICommand ToggleDeviceSwitcherCommand { get; }
        public ICommand ToggleGroupsExpandedCommand { get; }
        public ICommand NavigateDivisionSupportCommand { get; }
        public ICommand SelectDeviceCommand { get; }
        public ICommand CopyPrimaryCommand { get; }
        public ICommand CopyTextCommand { get; }
        public ICommand CopySummaryCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand LookupInAdCommand { get; }
        public ICommand LookupUserInAdCommand { get; }
        public ICommand LookupGroupInAdCommand { get; }
        public ICommand ToggleGroupFilterCommand { get; }
        public ICommand ClearGroupFilterCommand { get; }
        public ICommand ToggleDirectOnlyCommand { get; }

        public EntraHistoryItemViewModel(
            string query,
            EntraDeviceSearchResult? result,
            IEntraService entraService,
            IDeepLinkRoutingService linkRouter)
        {
            Query = query;
            Timestamp = DateTime.Now;
            _entraService = entraService ?? throw new ArgumentNullException(nameof(entraService));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            ToggleExpandCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
            ToggleDeviceSwitcherCommand = new RelayCommand(_ => IsDeviceSwitcherExpanded = !IsDeviceSwitcherExpanded);
            ToggleGroupsExpandedCommand = new RelayCommand(_ => IsGroupsExpanded = !IsGroupsExpanded);
            NavigateDivisionSupportCommand = new RelayCommand(_ =>
            {
                if (HasDivision)
                {
                    _linkRouter.RequestNavigation("group/division", DivisionCode);
                }
            });
            SelectDeviceCommand = new RelayCommand(param =>
            {
                if (param is EntraDeviceRecordOption option)
                {
                    SelectedDevice = option.Device;
                }
                else if (param is EntraDeviceInfo device)
                {
                    SelectedDevice = device;
                }
            });
            CopyPrimaryCommand = new RelayCommand(_ => CopyDeviceName());
            CopyTextCommand = new RelayCommand(param =>
            {
                if (param is string text && !string.IsNullOrWhiteSpace(text))
                {
                    CopyValue(text);
                }
            });
            CopySummaryCommand = new RelayCommand(_ => CopySummary());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            RemoveCommand = new RelayCommand(_ => RemoveRequested?.Invoke(this));
            LookupInAdCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(DeviceName))
                {
                    _linkRouter.RequestNavigation("computer", DeviceName);
                }
            });
            LookupUserInAdCommand = new RelayCommand(param =>
            {
                if (param is EntraDeviceOwnerInfo person)
                {
                    string? netId = ExtractNetId(person);
                    if (!string.IsNullOrWhiteSpace(netId))
                    {
                        _linkRouter.RequestNavigation("user", netId);
                    }
                }
            });
            LookupGroupInAdCommand = new RelayCommand(param =>
            {
                if (param is EntraDeviceGroupInfo group && group.IsSyncedFromOnPrem && !string.IsNullOrWhiteSpace(group.DisplayName))
                {
                    _linkRouter.RequestNavigation("group", $"members/{group.DisplayName}");
                }
            });
            ToggleGroupFilterCommand = new RelayCommand(_ => IsGroupFilterOpen = !IsGroupFilterOpen);
            ClearGroupFilterCommand = new RelayCommand(_ => GroupFilter = string.Empty);
            ToggleDirectOnlyCommand = new RelayCommand(_ => ShowDirectOnly = !ShowDirectOnly);

            ApplySearchResult(result);
        }

        private void ApplySearchResult(EntraDeviceSearchResult? result)
        {
            Devices.Clear();
            DeviceOptions.Clear();

            if (result is { Exists: true, Devices.Count: > 0 })
            {
                IsFound = true;
                ErrorMessage = null;
                foreach (var device in result.Devices)
                {
                    Devices.Add(device);
                }
                _selectedDevice = Devices[0];
                foreach (var device in Devices)
                {
                    DeviceOptions.Add(new EntraDeviceRecordOption(device, ReferenceEquals(device, _selectedDevice)));
                }
            }
            else
            {
                IsFound = false;
                ErrorMessage = result?.ErrorMessage ?? $"No Entra device found matching '{Query}'.";
                _selectedDevice = null;
            }

            OnPropertyChanged(nameof(HasMultipleDevices));
            OnPropertyChanged(nameof(MatchedDeviceCount));
            SyncActiveDeviceState();
        }

        private void SyncActiveDeviceState()
        {
            foreach (var option in DeviceOptions)
            {
                option.IsSelected = ReferenceEquals(option.Device, SelectedDevice);
            }

            Owners.Clear();
            AllGroups.Clear();

            if (SelectedDevice != null)
            {
                foreach (var owner in SelectedDevice.Owners)
                {
                    Owners.Add(owner);
                }

                AllGroups.AddRange(SelectedDevice.Groups);
            }

            var (divCode, divSource) = ExtractDivisionFromGroups(AllGroups);
            DivisionCode = divCode;
            DivisionSourceGroup = divSource;
            OnPropertyChanged(nameof(HasDivision));

            OnPropertyChanged(nameof(SelectedDevice));
            OnPropertyChanged(nameof(DeviceName));
            OnPropertyChanged(nameof(ObjectId));
            OnPropertyChanged(nameof(DeviceId));
            OnPropertyChanged(nameof(OperatingSystem));
            OnPropertyChanged(nameof(OperatingSystemVersion));
            OnPropertyChanged(nameof(OperatingSystemDisplay));
            OnPropertyChanged(nameof(HardwareDisplay));
            OnPropertyChanged(nameof(HasHardware));
            OnPropertyChanged(nameof(MdmDisplayName));
            OnPropertyChanged(nameof(MdmAppId));
            OnPropertyChanged(nameof(HasMdmAppId));
            OnPropertyChanged(nameof(IsManaged));
            OnPropertyChanged(nameof(DeviceOwnership));
            OnPropertyChanged(nameof(EnrollmentType));
            OnPropertyChanged(nameof(HasOwnership));
            OnPropertyChanged(nameof(HasEnrollmentType));
            OnPropertyChanged(nameof(JoinTypeDisplay));
            OnPropertyChanged(nameof(IsHybridJoined));
            OnPropertyChanged(nameof(IsEntraJoined));
            OnPropertyChanged(nameof(IsEntraRegistered));
            OnPropertyChanged(nameof(OnPremSyncDisplay));
            OnPropertyChanged(nameof(LastActivityDisplay));
            OnPropertyChanged(nameof(RegistrationDateDisplay));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(IsDisabled));
            OnPropertyChanged(nameof(HasOwners));
            OnPropertyChanged(nameof(OwnerLookupError));
            OnPropertyChanged(nameof(HasOwnerLookupError));
            OnPropertyChanged(nameof(TotalGroupCount));
            OnPropertyChanged(nameof(DirectGroupCount));
            OnPropertyChanged(nameof(HasGroups));
            OnPropertyChanged(nameof(GroupLookupError));
            OnPropertyChanged(nameof(HasGroupLookupError));
            OnPropertyChanged(nameof(CollapsedSummaryText));

            ApplyGroupFilter();
        }

        private void ApplyGroupFilter()
        {
            FilteredGroups.Clear();
            string term = GroupFilter.Trim();

            foreach (var g in AllGroups)
            {
                if (ShowDirectOnly && !g.IsDirect) continue;
                if (!string.IsNullOrEmpty(term) &&
                    !g.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                    !(g.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) &&
                    !g.ObjectId.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FilteredGroups.Add(g);
            }

            OnPropertyChanged(nameof(FilteredGroupCount));
            OnPropertyChanged(nameof(HasFilteredGroups));
        }

        private void CopyDeviceName()
        {
            if (string.IsNullOrWhiteSpace(DeviceName)) return;
            try
            {
                Clipboard.SetDataObject(DeviceName);
                IsCopied = true;
                _ = Task.Delay(1500).ContinueWith(_ => UiNotify.RunOnUiAsync(() => IsCopied = false));
            }
            catch { /* Clipboard access can fail if occupied */ }
        }

        private static void CopyValue(string value)
        {
            try
            {
                Clipboard.SetDataObject(value);
                UiNotify.Info($"Copied '{value}' to clipboard.", showStatusBar: true);
            }
            catch { /* ignored */ }
        }

        public void CopySummary()
        {
            try
            {
                var builder = EntraCardClipboardHelper.BuildSummary(this);
                if (!builder.CopyToClipboard()) return;
                IsSummaryCopied = true;
                UiNotify.Success($"Copied Entra summary for '{DeviceName}' to clipboard.", showStatusBar: true);
                _ = Task.Delay(1500).ContinueWith(_ => UiNotify.RunOnUiAsync(() => IsSummaryCopied = false));
            }
            catch (Exception ex)
            {
                Log.Warn("EntraHistoryItem", $"Failed to copy summary for '{DeviceName}': {ex.Message}");
                UiNotify.Warn($"Could not copy summary: {ex.Message}");
            }
        }

        private async Task RefreshAsync()
        {
            if (IsRefreshing || string.IsNullOrWhiteSpace(Query)) return;
            try
            {
                IsRefreshing = true;
                string? previouslySelectedObjectId = SelectedDevice?.ObjectId;
                var updated = await _entraService.SearchDevicesAsync(Query);
                ApplySearchResult(updated);

                if (!string.IsNullOrWhiteSpace(previouslySelectedObjectId))
                {
                    var match = Devices.FirstOrDefault(d => string.Equals(d.ObjectId, previouslySelectedObjectId, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        SelectedDevice = match;
                    }
                }

                Timestamp = DateTime.Now;
                OnPropertyChanged(nameof(TimeFormatted));
                OnPropertyChanged(nameof(RelativeAgeText));
                OnPropertyChanged(nameof(IsStale));
            }
            catch (Exception ex)
            {
                Log.Error("EntraHistoryItem", $"Failed to refresh Entra device '{Query}'", ex);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private static string? ExtractNetId(EntraDeviceOwnerInfo person)
        {
            string? source = !string.IsNullOrWhiteSpace(person.UserPrincipalName)
                ? person.UserPrincipalName
                : person.Mail;

            if (string.IsNullOrWhiteSpace(source)) return null;
            int atIndex = source.IndexOf('@');
            return atIndex > 0 ? source[..atIndex] : source;
        }

        private static (string Code, string SourceGroup) ExtractDivisionFromGroups(IEnumerable<EntraDeviceGroupInfo> groups)
        {
            var ordered = groups
                .Where(g => !string.IsNullOrWhiteSpace(g.DisplayName))
                .OrderByDescending(g => g.DisplayName.Contains("-Wrkst-Intune-All", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(g => g.IsDirect)
                .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase);

            foreach (var group in ordered)
            {
                string name = group.DisplayName.Trim();
                int idx = name.IndexOf("-Wrkst-Intune", StringComparison.OrdinalIgnoreCase);
                if (idx >= 2)
                {
                    string prefix = name[..idx].Trim();
                    if (prefix.All(char.IsLetterOrDigit))
                    {
                        string code = prefix.Length >= 4
                            ? prefix[..4].ToUpperInvariant()
                            : prefix.ToUpperInvariant();
                        return (code, name);
                    }
                }
            }

            return (string.Empty, string.Empty);
        }

        private static string FormatTimestamp(DateTimeOffset? value)
        {
            if (!value.HasValue) return "Never / Unknown";
            var local = value.Value.ToLocalTime();
            var age = DateTimeOffset.Now - local;
            string relative = age.TotalMinutes < 1
                ? "just now"
                : age.TotalHours < 1
                    ? $"{(int)age.TotalMinutes}m ago"
                    : age.TotalDays < 1
                        ? $"{(int)age.TotalHours}h ago"
                        : $"{(int)age.TotalDays}d ago";
            return $"{local:MMM dd, yyyy h:mm tt} ({relative})";
        }
    }
}
