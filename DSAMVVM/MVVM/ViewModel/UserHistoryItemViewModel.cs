using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.MVVM.ViewModel
{
    public class UserHistoryItemViewModel : ObservableObject
    {
        private readonly IADService _adService;
        private readonly IDepartmentService _deptService;
        private readonly IDeepLinkRoutingService _linkRouter;

        public string Query { get; }
        public DateTime Timestamp { get; private set; }
        public string TimeFormatted => Timestamp.ToString("h:mm tt");

        public string RelativeAgeText
        {
            get
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalMinutes < 1) return "just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
                return $"{(int)diff.TotalHours}h ago";
            }
        }

        public bool IsStale => (DateTime.Now - Timestamp).TotalMinutes >= 5;

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStale));
                    OnPropertyChanged(nameof(RelativeAgeText));
                }
            }
        }

        private ADUserInfo? _user;
        public ADUserInfo? User
        {
            get => _user;
            private set
            {
                _user = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFound));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(NetId));
                OnPropertyChanged(nameof(Initials));
                OnPropertyChanged(nameof(HasInitials));
                OnPropertyChanged(nameof(Affiliation));
                OnPropertyChanged(nameof(DepartmentDisplay));
                OnPropertyChanged(nameof(DivisionDisplay));
                OnPropertyChanged(nameof(DivisionCode));
                OnPropertyChanged(nameof(HasDivision));
                OnPropertyChanged(nameof(HasNoDivisionMimWarning));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(IsDisabled));
                OnPropertyChanged(nameof(HasMimWarning));
                OnPropertyChanged(nameof(LicenseSummary));
                OnPropertyChanged(nameof(RawLicense));
            }
        }

        public bool IsFound => User?.Exists == true;
        public string? ErrorMessage => User?.ErrorMessage;

        public string DisplayName => !string.IsNullOrWhiteSpace(User?.DisplayName)
            ? User.DisplayName
            : (!string.IsNullOrWhiteSpace(User?.Name) ? User.Name : Query);

        public string NetId => User?.Name ?? Query;
        public string Initials => CalculateInitials(DisplayName);
        public bool HasInitials => !string.IsNullOrWhiteSpace(Initials) && Initials.Length == 2;
        public string Affiliation => User?.EduAffiliation ?? string.Empty;

        public string DepartmentDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(User?.DepartmentName) && !string.IsNullOrEmpty(User?.DepartmentNumber))
                    return $"{User.DepartmentName} ({User.DepartmentNumber})";
                if (!string.IsNullOrEmpty(User?.DepartmentName))
                    return User.DepartmentName;
                if (!string.IsNullOrEmpty(User?.DepartmentNumber))
                    return $"Dept #{User.DepartmentNumber}";
                return "No Department";
            }
        }

        public string DivisionDisplay => !string.IsNullOrEmpty(User?.Division) ? User.Division : string.Empty;
        public string DivisionCode => !string.IsNullOrWhiteSpace(User?.Division) ? User.Division : "N/A";
        
        public bool HasDivision => !string.IsNullOrWhiteSpace(User?.Division) &&
                                   !User.Division.Equals("No Departmental MIM group", StringComparison.OrdinalIgnoreCase) &&
                                   !User.Division.Equals("N/A", StringComparison.OrdinalIgnoreCase);

        public bool HasNoDivisionMimWarning => User?.Exists == true &&
                                               (string.IsNullOrWhiteSpace(User.Division) ||
                                                User.Division.Equals("No Departmental MIM group", StringComparison.OrdinalIgnoreCase));

        public bool IsActive => User?.Exists == true && User.Enabled != false && User.Locked != true;
        public bool IsLocked => User?.Locked == true;
        public bool IsDisabled => User?.Enabled == false;
        public bool HasMimWarning => User is { Exists: true, HasMimWrkstGroup: false };

        public string LicenseSummary => !string.IsNullOrWhiteSpace(User?.License) ? User.License : "None";
        public string? RawLicense => User?.RawLicense;

        // Support & Department Context
        private string? _supportTeamName;
        public string? SupportTeamName
        {
            get => _supportTeamName;
            private set { _supportTeamName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSupportTeam)); }
        }
        public bool HasSupportTeam => !string.IsNullOrWhiteSpace(SupportTeamName);

        private string? _managerName;
        public string? ManagerName
        {
            get => _managerName;
            private set { _managerName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasManager)); }
        }
        public bool HasManager => !string.IsNullOrWhiteSpace(ManagerName);

        private string? _managerNetId;
        public string? ManagerNetID
        {
            get => _managerNetId;
            private set { _managerNetId = value; OnPropertyChanged(); }
        }

        private string? _supportPhone;
        public string? SupportPhone
        {
            get => _supportPhone;
            private set { _supportPhone = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSupportPhone)); }
        }
        public bool HasSupportPhone => !string.IsNullOrWhiteSpace(SupportPhone);

        private string? _departmentNotes;
        public string? DepartmentNotes
        {
            get => _departmentNotes;
            private set { _departmentNotes = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNotes)); }
        }
        public bool HasNotes => !string.IsNullOrWhiteSpace(DepartmentNotes);

        private string? _fileRepoPath;
        public string? FileRepoPath
        {
            get => _fileRepoPath;
            private set { _fileRepoPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFileRepo)); }
        }
        public bool HasFileRepo => !string.IsNullOrWhiteSpace(FileRepoPath);

        // Optional Team Details Drawer (Manager, Phone)
        private bool _showTeamDetails;
        public bool ShowTeamDetails
        {
            get => _showTeamDetails;
            set { _showTeamDetails = value; OnPropertyChanged(); }
        }

        // Adobe License State
        private bool _isAdobeChecking;
        public bool IsAdobeChecking
        {
            get => _isAdobeChecking;
            private set { _isAdobeChecking = value; OnPropertyChanged(); }
        }

        private AdobeLicenseStatus? _adobeStatus;
        public AdobeLicenseStatus? AdobeStatus
        {
            get => _adobeStatus;
            private set
            {
                _adobeStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAdobeChecked));
                OnPropertyChanged(nameof(HasAcrobatPro));
                OnPropertyChanged(nameof(HasCreativeCloud));
            }
        }
        public bool HasAdobeChecked => AdobeStatus != null;
        public bool HasAcrobatPro => AdobeStatus?.HasAcrobatPro == true;
        public bool HasCreativeCloud => AdobeStatus?.HasCreativeCloud == true;

        private string? _adobeCheckTime;
        public string? AdobeCheckTime
        {
            get => _adobeCheckTime;
            private set { _adobeCheckTime = value; OnPropertyChanged(); }
        }

        // MIM Groups Inspection State
        private bool _isMimLoading;
        public bool IsMimLoading
        {
            get => _isMimLoading;
            private set { _isMimLoading = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> MimGroups { get; } = [];

        private bool _showMimGroups;
        public bool ShowMimGroups
        {
            get => _showMimGroups;
            set { _showMimGroups = value; OnPropertyChanged(); }
        }

        private string? _mimGroupsStatusMessage;
        public string? MimGroupsStatusMessage
        {
            get => _mimGroupsStatusMessage;
            private set { _mimGroupsStatusMessage = value; OnPropertyChanged(); }
        }

        // Raw License View Toggle
        private bool _showRawLicense;
        public bool ShowRawLicense
        {
            get => _showRawLicense;
            set { _showRawLicense = value; OnPropertyChanged(); }
        }

        // Copy Feedback
        private bool _isCopied;
        public bool IsCopied
        {
            get => _isCopied;
            private set { _isCopied = value; OnPropertyChanged(); }
        }

        // Refresh State
        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set { _isRefreshing = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand ToggleExpandCommand { get; }
        public ICommand CopyNetIdCommand { get; }
        public ICommand CheckAdobeCommand { get; }
        public ICommand OpenFileRepoCommand { get; }
        public ICommand ToggleRawLicenseCommand { get; }
        public ICommand ToggleMimGroupsCommand { get; }
        public ICommand ToggleTeamDetailsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand NavigateTeamCommand { get; }
        public ICommand NavigateDivisionSupportCommand { get; }

        public UserHistoryItemViewModel(
            string query,
            ADUserInfo? user,
            IADService adService,
            IDepartmentService deptService,
            IDeepLinkRoutingService linkRouter)
        {
            Query = query;
            Timestamp = DateTime.Now;
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            User = user;

            ToggleExpandCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
            CopyNetIdCommand = new RelayCommand(_ => CopyNetId());
            CheckAdobeCommand = new RelayCommand(async _ => await CheckAdobeAsync());
            OpenFileRepoCommand = new RelayCommand(_ => OpenFileRepo());
            ToggleRawLicenseCommand = new RelayCommand(_ => ShowRawLicense = !ShowRawLicense);
            ToggleMimGroupsCommand = new RelayCommand(async _ => await ToggleMimGroupsAsync());
            ToggleTeamDetailsCommand = new RelayCommand(_ => ShowTeamDetails = !ShowTeamDetails);
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            NavigateTeamCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(SupportTeamName))
                {
                    _linkRouter.RequestNavigation("GroupView", SupportTeamName);
                }
            });
            NavigateDivisionSupportCommand = new RelayCommand(_ =>
            {
                if (HasDivision)
                {
                    _linkRouter.RequestNavigation("GroupView", DivisionCode);
                }
            });
        }

        public async Task LoadDepartmentDetailsAsync()
        {
            if (User == null || string.IsNullOrWhiteSpace(User.DepartmentNumber)) return;

            try
            {
                var dept = await _deptService.GetDepartmentAsync(User.DepartmentNumber);
                DepartmentNotes = dept?.Notes;
                FileRepoPath = await _deptService.GetFileRepoPathAsync(User.DepartmentNumber);

                var teamName = await _deptService.GetTeamAsync(User.DepartmentNumber);
                SupportTeamName = teamName?.Trim();

                if (!string.IsNullOrWhiteSpace(SupportTeamName))
                {
                    var teamInfo = await _deptService.GetSupportTeamAsync(SupportTeamName);
                    if (teamInfo != null)
                    {
                        ManagerName = teamInfo.ManagerName;
                        ManagerNetID = teamInfo.ManagerNetID;
                        SupportPhone = teamInfo.PhoneNumber;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("UserHistoryItem", $"Failed to load department details: {ex.Message}");
            }
        }

        private async Task ToggleMimGroupsAsync()
        {
            ShowMimGroups = !ShowMimGroups;
            if (ShowMimGroups && MimGroups.Count == 0 && !IsMimLoading && !string.IsNullOrWhiteSpace(NetId))
            {
                try
                {
                    IsMimLoading = true;
                    MimGroupsStatusMessage = "Loading MIM groups…";
                    var result = await _adService.GetUserMimGroupsAsync(NetId);
                    MimGroups.Clear();
                    if (result.Groups is { Count: > 0 })
                    {
                        foreach (var g in result.Groups)
                        {
                            MimGroups.Add(g);
                        }
                        MimGroupsStatusMessage = null;
                    }
                    else
                    {
                        MimGroupsStatusMessage = "No valid MIM groups found for user.";
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("UserHistoryItem", $"Failed to load MIM groups for '{NetId}'", ex);
                    MimGroupsStatusMessage = $"Error loading MIM groups: {ex.Message}";
                }
                finally
                {
                    IsMimLoading = false;
                }
            }
        }

        private async Task RefreshAsync()
        {
            if (IsRefreshing) return;
            try
            {
                IsRefreshing = true;
                var updatedUser = await _adService.GetUserAsync(NetId);
                User = updatedUser;
                Timestamp = DateTime.Now;
                OnPropertyChanged(nameof(TimeFormatted));
                OnPropertyChanged(nameof(RelativeAgeText));
                OnPropertyChanged(nameof(IsStale));
                await LoadDepartmentDetailsAsync();

                if (HasAdobeChecked)
                {
                    await CheckAdobeAsync();
                }

                if (ShowMimGroups)
                {
                    MimGroups.Clear();
                    await ToggleMimGroupsAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error("UserHistoryItem", $"Failed to refresh user '{NetId}'", ex);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task CheckAdobeAsync()
        {
            if (string.IsNullOrWhiteSpace(NetId) || IsAdobeChecking) return;
            try
            {
                IsAdobeChecking = true;
                var status = await _adService.CheckAdobeLicensesAsync(NetId);
                AdobeStatus = status;
                AdobeCheckTime = DateTime.Now.ToString("h:mm tt");
            }
            catch (Exception ex)
            {
                Log.Error("UserHistoryItem", $"Adobe check failed for '{NetId}'", ex);
            }
            finally
            {
                IsAdobeChecking = false;
            }
        }

        private void CopyNetId()
        {
            if (string.IsNullOrWhiteSpace(NetId)) return;
            try
            {
                Clipboard.SetDataObject(NetId);
                IsCopied = true;
                _ = Task.Delay(1500).ContinueWith(_ => UiNotify.RunOnUiAsync(() => IsCopied = false));
            }
            catch { /* Clipboard access can fail if occupied */ }
        }

        private void OpenFileRepo()
        {
            if (string.IsNullOrWhiteSpace(FileRepoPath)) return;
            try
            {
                Process.Start(new ProcessStartInfo(FileRepoPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warn("UserHistoryItem", $"Failed to open file repo path '{FileRepoPath}': {ex.Message}");
                UiNotify.Warn($"Could not open repository: {ex.Message}");
            }
        }

        private static string CalculateInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            // 1. Strip any parenthesized, bracketed, or angle-bracketed content e.g. "(ibroomall)", "[Staff]"
            var cleaned = Regex.Replace(name, @"[\(\[\<].*?[\)\]\>]", "").Trim();

            // 2. Remove any non-letter characters except comma, space, hyphen
            cleaned = Regex.Replace(cleaned, @"[^\p{L}\s,\-]", "").Trim();

            if (string.IsNullOrWhiteSpace(cleaned)) return "";

            // 3. Handle "Last, First" format (standard in Active Directory displayName)
            if (cleaned.Contains(','))
            {
                var parts = cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    char first = GetFirstLetter(parts[1]);
                    char last = GetFirstLetter(parts[0]);
                    if (first != '\0' && last != '\0')
                        return $"{first}{last}";
                    if (last != '\0') return last.ToString();
                }
            }

            // 4. Handle "First Last" format
            var words = cleaned.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length >= 2)
            {
                char first = GetFirstLetter(words[0]);
                char last = GetFirstLetter(words[^1]);
                if (first != '\0' && last != '\0')
                    return $"{first}{last}";
            }
            else if (words.Length == 1)
            {
                char first = GetFirstLetter(words[0]);
                if (first != '\0')
                    return first.ToString();
            }

            return "";
        }

        private static char GetFirstLetter(string str)
        {
            return (from c in str where char.IsLetter(c) select char.ToUpperInvariant(c)).FirstOrDefault();
        }
    }
}
