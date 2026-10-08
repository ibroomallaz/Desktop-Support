using DSAMVVM.MVVM.ViewModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.Core.Formatters;
using DSAMVVM.MVVM.Model.AD;
using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.MVVM.ViewModel.Cards
{
    public class SupportTeamCardItem : ObservableObject
    {
        public string TeamName { get; init; }
        public string ManagerName { get; init; }
        public string ManagerNetId { get; init; }
        public bool HasManager => !string.IsNullOrWhiteSpace(ManagerName) || !string.IsNullOrWhiteSpace(ManagerNetId);
        public string? PhoneNumber { get; init; }
        public bool HasPhone => !string.IsNullOrWhiteSpace(PhoneNumber);
        public List<string> SupportedDivisions { get; init; }
        public bool HasDivisions => SupportedDivisions.Count > 0;
        public string DivisionsToggleLabel => $"{SupportedDivisions.Count} Supported Divisions";

        private bool _showDivisions;
        public bool ShowDivisions
        {
            get => _showDivisions;
            set { _showDivisions = value; OnPropertyChanged(); }
        }

        public ICommand LookupManagerCommand { get; }
        public ICommand CopyTeamCommand { get; }
        public ICommand CopyManagerNetIdCommand { get; }
        public ICommand ToggleDivisionsCommand { get; }

        public SupportTeamCardItem(
            string teamName,
            string managerName,
            string managerNetId,
            string? phoneNumber,
            List<string> supportedDivisions,
            IDeepLinkRoutingService linkRouter)
        {
            TeamName = teamName;
            ManagerName = managerName;
            ManagerNetId = managerNetId;
            PhoneNumber = phoneNumber;
            SupportedDivisions = supportedDivisions;

            LookupManagerCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(ManagerNetId))
                {
                    linkRouter.RequestNavigation("user", ManagerNetId);
                }
            });

            CopyTeamCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(TeamName))
                {
                    try
                    {
                        Clipboard.SetDataObject(TeamName);
                        UiNotify.Info($"Copied '{TeamName}' to clipboard.", showStatusBar: true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            });

            CopyManagerNetIdCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(ManagerNetId))
                {
                    try
                    {
                        Clipboard.SetDataObject(ManagerNetId);
                        UiNotify.Info($"Copied '{ManagerNetId}' to clipboard.", showStatusBar: true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            });

            ToggleDivisionsCommand = new RelayCommand(_ => ShowDivisions = !ShowDivisions);
        }
    }

    public class GroupHistoryItemViewModel : ObservableObject
    {
        private readonly IADService _adService;
        private readonly IDepartmentService _deptService;
        private readonly IDeepLinkRoutingService _linkRouter;

        public event Action<GroupHistoryItemViewModel>? RemoveRequested;

        public GroupViewModel.GroupSearchMode Mode { get; }
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

        // Mode flags
        public bool IsUserMimMode => Mode == GroupViewModel.GroupSearchMode.UserMim;
        public bool IsGroupMembersMode => Mode == GroupViewModel.GroupSearchMode.GroupMembers;
        public bool IsDeptMode => Mode == GroupViewModel.GroupSearchMode.Department;
        public bool IsDivMode => Mode == GroupViewModel.GroupSearchMode.Division;

        public string ModeBadgeText => Mode switch
        {
            GroupViewModel.GroupSearchMode.UserMim => "User MIM",
            GroupViewModel.GroupSearchMode.GroupMembers => "Group Members",
            GroupViewModel.GroupSearchMode.Department => "Dept Support",
            GroupViewModel.GroupSearchMode.Division => "Division Support",
            _ => "Group"
        };

        public string ModeGlyph => Mode switch
        {
            GroupViewModel.GroupSearchMode.UserMim => "\uE77B", // User
            GroupViewModel.GroupSearchMode.GroupMembers => "\uE902", // Group
            GroupViewModel.GroupSearchMode.Department => "\uE82D", // Server / Org
            GroupViewModel.GroupSearchMode.Division => "\uE774", // Globe / Org
            _ => "\uE902"
        };

        // Common State
        private bool _isFound;
        public bool IsFound { get => _isFound; private set { _isFound = value; OnPropertyChanged(); } }

        private string? _errorMessage;
        public string? ErrorMessage { get => _errorMessage; private set { _errorMessage = value; OnPropertyChanged(); } }

        private bool _isRefreshing;
        public bool IsRefreshing { get => _isRefreshing; private set { _isRefreshing = value; OnPropertyChanged(); } }

        private bool _isCopied;
        public bool IsCopied { get => _isCopied; private set { _isCopied = value; OnPropertyChanged(); } }
        private bool _isSummaryCopied;
        public bool IsSummaryCopied { get => _isSummaryCopied; private set { _isSummaryCopied = value; OnPropertyChanged(); } }

        // --- Mode 1: User MIM Groups ---
        public List<string> AllMimGroups { get; } = [];
        public ObservableCollection<string> MimGroups { get; } = [];
        public int MimGroupCount => AllMimGroups.Count;
        public int FilteredMimGroupCount => MimGroups.Count;
        public bool HasMimGroups => AllMimGroups.Count > 0;

        private bool _isMimFilterOpen;
        public bool IsMimFilterOpen
        {
            get => _isMimFilterOpen;
            set { _isMimFilterOpen = value; OnPropertyChanged(); }
        }

        private string _mimFilter = string.Empty;
        public string MimFilter
        {
            get => _mimFilter;
            set
            {
                if (Set(ref _mimFilter, value))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        IsMimFilterOpen = true;
                    }
                    ApplyMimFilter();
                }
            }
        }

        private bool? _userEnabled;
        public bool? UserEnabled { get => _userEnabled; private set { _userEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsUserDisabled)); } }
        public bool IsUserDisabled => UserEnabled == false;
        public bool HasMimWarning => IsUserMimMode && IsFound && !AllMimGroups.Any(g => g.Equals("UA-MIM-Wrkst-AllDivUsers", StringComparison.OrdinalIgnoreCase));

        // --- Mode 2: Group Members ---
        public string NormalizedGroupName { get; private init; } = string.Empty;
        public List<string> AllGroupMembers { get; } = [];
        public ObservableCollection<string> FilteredMembers { get; } = [];
        public int MemberCount => AllGroupMembers.Count;
        public int FilteredMemberCount => FilteredMembers.Count;
        public bool HasMembers => AllGroupMembers.Count > 0;

        private bool _isMemberFilterOpen;
        public bool IsMemberFilterOpen
        {
            get => _isMemberFilterOpen;
            set { _isMemberFilterOpen = value; OnPropertyChanged(); }
        }

        private string _memberFilter = string.Empty;
        public string MemberFilter
        {
            get => _memberFilter;
            set
            {
                if (Set(ref _memberFilter, value))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        IsMemberFilterOpen = true;
                    }
                    ApplyMemberFilter();
                }
            }
        }

        // --- Mode 3: Department Details ---
        public string DeptNumber { get; private init; } = string.Empty;
        private string? _deptTeamName;
        public string? DeptTeamName
        {
            get => _deptTeamName;
            private set
            {
                _deptTeamName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDeptTeam));
                OnPropertyChanged(nameof(DeptTeamDisplayName));
            }
        }
        public bool HasDeptTeam => !string.IsNullOrWhiteSpace(DeptTeamName);
        public string DeptTeamDisplayName => UserHistoryItemViewModel.FormatMiddleTruncate(DeptTeamName, 24);

        private string? _deptManagerName;
        public string? DeptManagerName { get => _deptManagerName; private set { _deptManagerName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDeptManager)); } }

        private string? _deptManagerNetId;
        public string? DeptManagerNetId { get => _deptManagerNetId; private set { _deptManagerNetId = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDeptManager)); } }
        public bool HasDeptManager => !string.IsNullOrWhiteSpace(DeptManagerName) || !string.IsNullOrWhiteSpace(DeptManagerNetId);

        private string? _deptSupportPhone;
        public string? DeptSupportPhone { get => _deptSupportPhone; private set { _deptSupportPhone = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDeptPhone)); } }
        public bool HasDeptPhone => !string.IsNullOrWhiteSpace(DeptSupportPhone);

        private string? _deptNotes;
        public string? DeptNotes { get => _deptNotes; private set { _deptNotes = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDeptNotes)); } }
        public bool HasDeptNotes => !string.IsNullOrWhiteSpace(DeptNotes);

        private string? _deptFileRepoPath;
        public string? DeptFileRepoPath { get => _deptFileRepoPath; private set { _deptFileRepoPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDeptFileRepo)); } }
        public bool HasDeptFileRepo => !string.IsNullOrWhiteSpace(DeptFileRepoPath);

        private bool _showTeamDetails = true;
        public bool ShowTeamDetails
        {
            get => _showTeamDetails;
            set { _showTeamDetails = value; OnPropertyChanged(); }
        }

        // --- Mode 4: Division Details ---
        public string DivCode { get; private init; } = string.Empty;
        public ObservableCollection<SupportTeamCardItem> DivTeams { get; } = [];
        public int DivTeamCount => DivTeams.Count;
        public bool HasDivTeams => DivTeams.Count > 0;

        // Primary Header Title
        public string PrimaryHeaderTitle
        {
            get
            {
                return Mode switch
                {
                    GroupViewModel.GroupSearchMode.UserMim => Query,
                    GroupViewModel.GroupSearchMode.GroupMembers => !string.IsNullOrWhiteSpace(NormalizedGroupName) ? NormalizedGroupName : Query,
                    GroupViewModel.GroupSearchMode.Department => $"Department {DeptNumber}",
                    GroupViewModel.GroupSearchMode.Division => $"Division {DivCode}",
                    _ => Query
                };
            }
        }

        // Collapsed summary text
        public string CollapsedSummaryText
        {
            get
            {
                return Mode switch
                {
                    GroupViewModel.GroupSearchMode.UserMim => $"{MimGroupCount} MIM Groups",
                    GroupViewModel.GroupSearchMode.GroupMembers => $"{MemberCount} Members",
                    GroupViewModel.GroupSearchMode.Department => HasDeptTeam ? $"Support: {DeptTeamName}" : (IsFound ? "No Team Assigned" : "Not Found"),
                    GroupViewModel.GroupSearchMode.Division => $"{DivTeamCount} Support Teams",
                    _ => Query
                };
            }
        }

        // Commands
        public ICommand ToggleExpandCommand { get; }
        public ICommand CopyPrimaryCommand { get; }
        public ICommand CopySummaryCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ToggleTeamDetailsCommand { get; }
        public ICommand OpenFileRepoCommand { get; }
        public ICommand LookupUserCommand { get; }
        public ICommand LookupGroupCommand { get; }
        public ICommand CopyItemCommand { get; }
        public ICommand LookupManagerCommand { get; }
        public ICommand ClearMimFilterCommand { get; }
        public ICommand ClearMemberFilterCommand { get; }
        public ICommand ToggleMimFilterCommand { get; }
        public ICommand ToggleMemberFilterCommand { get; }
        public ICommand RemoveCommand { get; }

        public GroupHistoryItemViewModel(
            GroupViewModel.GroupSearchMode mode,
            string query,
            IADService adService,
            IDepartmentService deptService,
            IDeepLinkRoutingService linkRouter)
        {
            Mode = mode;
            Query = query;
            Timestamp = DateTime.Now;
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            ToggleExpandCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
            CopyPrimaryCommand = new RelayCommand(_ => CopyPrimaryToClipboard());
            CopySummaryCommand = new RelayCommand(_ => CopySummary());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            ToggleTeamDetailsCommand = new RelayCommand(_ => ShowTeamDetails = !ShowTeamDetails);
            OpenFileRepoCommand = new RelayCommand(_ => OpenFileRepo());
            RemoveCommand = new RelayCommand(_ => RemoveRequested?.Invoke(this));

            LookupUserCommand = new RelayCommand(param =>
            {
                if (param is string netId && !string.IsNullOrWhiteSpace(netId))
                {
                    _linkRouter.RequestNavigation("user", netId.Trim());
                }
            });

            LookupGroupCommand = new RelayCommand(param =>
            {
                if (param is string grp && !string.IsNullOrWhiteSpace(grp))
                {
                    _linkRouter.RequestNavigation("group", grp.Trim());
                }
            });

            CopyItemCommand = new RelayCommand(param =>
            {
                if (param is string text && !string.IsNullOrWhiteSpace(text))
                {
                    try
                    {
                        Clipboard.SetDataObject(text.Trim());
                        UiNotify.Info($"Copied '{text}' to clipboard.", showStatusBar: true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            });

            LookupManagerCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(DeptManagerNetId))
                {
                    _linkRouter.RequestNavigation("user", DeptManagerNetId.Trim());
                }
            });

            ClearMimFilterCommand = new RelayCommand(_ => MimFilter = string.Empty);
            ClearMemberFilterCommand = new RelayCommand(_ => MemberFilter = string.Empty);
            ToggleMimFilterCommand = new RelayCommand(_ => IsMimFilterOpen = !IsMimFilterOpen);
            ToggleMemberFilterCommand = new RelayCommand(_ => IsMemberFilterOpen = !IsMemberFilterOpen);
        }

        public static GroupHistoryItemViewModel CreateUserMim(
            string netId,
            MimLookupResult? result,
            IADService ad,
            IDepartmentService dept,
            IDeepLinkRoutingService router)
        {
            var vm = new GroupHistoryItemViewModel(GroupViewModel.GroupSearchMode.UserMim, netId, ad, dept, router);
            vm.LoadMimResult(result);
            return vm;
        }

        public void LoadMimResult(MimLookupResult? result)
        {
            AllMimGroups.Clear();
            MimGroups.Clear();
            if (result is { Exists: true })
            {
                IsFound = true;
                UserEnabled = result.Enabled;
                ErrorMessage = null;
                AllMimGroups.AddRange(result.Groups);
                ApplyMimFilter();
            }
            else
            {
                IsFound = false;
                UserEnabled = null;
                ErrorMessage = string.IsNullOrWhiteSpace(result?.Error) ? $"'{Query}' is not a valid NetID or no MIM groups exist." : result.Error;
            }
            OnPropertyChanged(nameof(MimGroupCount));
            OnPropertyChanged(nameof(FilteredMimGroupCount));
            OnPropertyChanged(nameof(HasMimGroups));
            OnPropertyChanged(nameof(HasMimWarning));
            OnPropertyChanged(nameof(PrimaryHeaderTitle));
            OnPropertyChanged(nameof(CollapsedSummaryText));
        }

        public static GroupHistoryItemViewModel CreateGroupMembers(
            string query,
            string normalizedName,
            ADGroupInfo? result,
            IADService ad,
            IDepartmentService dept,
            IDeepLinkRoutingService router)
        {
            var vm = new GroupHistoryItemViewModel(GroupViewModel.GroupSearchMode.GroupMembers, query, ad, dept, router)
            {
                NormalizedGroupName = normalizedName
            };
            vm.LoadGroupResult(result);
            return vm;
        }

        public void LoadGroupResult(ADGroupInfo? result)
        {
            AllGroupMembers.Clear();
            FilteredMembers.Clear();
            if (result is { Exists: true })
            {
                IsFound = true;
                ErrorMessage = null;
                if (result.GroupMembers != null)
                {
                    AllGroupMembers.AddRange(result.GroupMembers);
                }
                ApplyMemberFilter();
            }
            else
            {
                IsFound = false;
                ErrorMessage = result?.ErrorMessage ?? $"Group '{NormalizedGroupName}' not found in Active Directory.";
            }
            OnPropertyChanged(nameof(MemberCount));
            OnPropertyChanged(nameof(FilteredMemberCount));
            OnPropertyChanged(nameof(HasMembers));
            OnPropertyChanged(nameof(PrimaryHeaderTitle));
            OnPropertyChanged(nameof(CollapsedSummaryText));
        }

        public static GroupHistoryItemViewModel CreateDepartment(
            string deptNum,
            IDepartment? dept,
            SupportTeam? team,
            IADService ad,
            IDepartmentService deptService,
            IDeepLinkRoutingService router)
        {
            var vm = new GroupHistoryItemViewModel(GroupViewModel.GroupSearchMode.Department, deptNum, ad, deptService, router)
            {
                DeptNumber = deptNum
            };
            vm.LoadDepartmentResult(dept, team);
            return vm;
        }

        public void LoadDepartmentResult(IDepartment? dept, SupportTeam? team)
        {
            if (dept != null)
            {
                IsFound = true;
                ErrorMessage = null;
                DeptNotes = dept.Notes;
                DeptFileRepoPath = dept.FileRepoPath;

                if (team != null)
                {
                    DeptTeamName = team.SupportTeamName;
                    DeptManagerName = team.ManagerName;
                    DeptManagerNetId = team.ManagerNetID;
                    DeptSupportPhone = team.PhoneNumber;
                }
                else
                {
                    DeptTeamName = dept.Team;
                    DeptManagerName = dept.ManagerName;
                    DeptManagerNetId = dept.ManagerNetId;
                    DeptSupportPhone = dept.SupportPhoneNumber;
                }
            }
            else
            {
                IsFound = false;
                ErrorMessage = $"Department '{DeptNumber}' not found in configuration.";
                DeptNotes = null;
                DeptFileRepoPath = null;
                DeptTeamName = null;
                DeptManagerName = null;
                DeptManagerNetId = null;
                DeptSupportPhone = null;
            }
            OnPropertyChanged(nameof(PrimaryHeaderTitle));
            OnPropertyChanged(nameof(CollapsedSummaryText));
        }

        public static GroupHistoryItemViewModel CreateDivision(
            string divCode,
            IEnumerable<SupportTeam>? teams,
            IADService ad,
            IDepartmentService deptService,
            IDeepLinkRoutingService router)
        {
            var vm = new GroupHistoryItemViewModel(GroupViewModel.GroupSearchMode.Division, divCode, ad, deptService, router)
            {
                DivCode = divCode.Trim().ToUpperInvariant()
            };
            vm.LoadDivisionResult(teams);
            return vm;
        }

        public void LoadDivisionResult(IEnumerable<SupportTeam>? teams)
        {
            DivTeams.Clear();
            var list = teams?.ToList() ?? [];
            if (list.Count > 0)
            {
                IsFound = true;
                ErrorMessage = null;
                foreach (var t in list)
                {
                    var divs = t.SupportedDivisions.Select(d => $"{d.DivAbbrev} - {d.DivFullName}").ToList();
                    DivTeams.Add(new SupportTeamCardItem(
                        t.SupportTeamName,
                        t.ManagerName,
                        t.ManagerNetID,
                        t.PhoneNumber,
                        divs,
                        _linkRouter));
                }
            }
            else
            {
                IsFound = false;
                ErrorMessage = $"No support teams configured for division '{DivCode}'.";
            }
            OnPropertyChanged(nameof(DivTeamCount));
            OnPropertyChanged(nameof(HasDivTeams));
            OnPropertyChanged(nameof(PrimaryHeaderTitle));
            OnPropertyChanged(nameof(CollapsedSummaryText));
        }

        private void ApplyMimFilter()
        {
            MimGroups.Clear();
            var filter = MimFilter.Trim();
            var matches = string.IsNullOrWhiteSpace(filter)
                ? AllMimGroups
                : AllMimGroups.Where(g => g.Contains(filter, StringComparison.OrdinalIgnoreCase));

            foreach (var g in matches)
            {
                MimGroups.Add(g);
            }
            OnPropertyChanged(nameof(FilteredMimGroupCount));
        }

        private void ApplyMemberFilter()
        {
            FilteredMembers.Clear();
            var filter = MemberFilter.Trim();
            var matches = string.IsNullOrWhiteSpace(filter)
                ? AllGroupMembers
                : AllGroupMembers.Where(m => m.Contains(filter, StringComparison.OrdinalIgnoreCase));

            foreach (var m in matches)
            {
                FilteredMembers.Add(m);
            }
            OnPropertyChanged(nameof(FilteredMemberCount));
        }

        public async Task RefreshAsync()
        {
            if (IsRefreshing) return;
            try
            {
                IsRefreshing = true;
                switch (Mode)
                {
                    case GroupViewModel.GroupSearchMode.UserMim:
                        var mim = await _adService.GetUserMimGroupsAsync(Query);
                        LoadMimResult(mim);
                        break;

                    case GroupViewModel.GroupSearchMode.GroupMembers:
                        var grp = await _adService.GetGroupAsync(NormalizedGroupName);
                        LoadGroupResult(grp);
                        break;

                    case GroupViewModel.GroupSearchMode.Department:
                        var dept = await _deptService.GetDepartmentAsync(DeptNumber);
                        SupportTeam? team = null;
                        if (dept != null)
                        {
                            var teamName = await _deptService.GetTeamAsync(dept.Number);
                            if (!string.IsNullOrWhiteSpace(teamName))
                            {
                                team = await _deptService.GetSupportTeamAsync(teamName.Trim());
                            }
                        }
                        LoadDepartmentResult(dept, team);
                        break;

                    case GroupViewModel.GroupSearchMode.Division:
                        var teams = await _deptService.GetTeamsByDivisionAsync(DivCode);
                        LoadDivisionResult(teams);
                        break;
                }

                Timestamp = DateTime.Now;
                OnPropertyChanged(nameof(TimeFormatted));
                OnPropertyChanged(nameof(RelativeAgeText));
                OnPropertyChanged(nameof(IsStale));
            }
            catch (Exception ex)
            {
                Log.Error("GroupHistoryItem", $"Refresh failed for '{Query}' ({Mode})", ex);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        public void CopySummary()
        {
            try
            {
                var builder = GroupCardClipboardHelper.BuildSummary(this);
                if (builder.CopyToClipboard())
                {
                    IsSummaryCopied = true;
                    UiNotify.Success($"Copied summary for '{PrimaryHeaderTitle}' to clipboard.", showStatusBar: true);
                    _ = Task.Delay(1500).ContinueWith(_ => UiNotify.RunOnUiAsync(() => IsSummaryCopied = false));
                }
            }
            catch (Exception ex)
            {
                Log.Warn("GroupHistoryItem", $"Failed to copy summary for '{PrimaryHeaderTitle}': {ex.Message}");
                UiNotify.Warn($"Could not copy summary: {ex.Message}");
            }
        }

        private void CopyPrimaryToClipboard()
        {
            string toCopy = Mode switch
            {
                GroupViewModel.GroupSearchMode.UserMim => Query,
                GroupViewModel.GroupSearchMode.GroupMembers => !string.IsNullOrWhiteSpace(NormalizedGroupName) ? NormalizedGroupName : Query,
                GroupViewModel.GroupSearchMode.Department => DeptNumber,
                GroupViewModel.GroupSearchMode.Division => DivCode,
                _ => Query
            };

            if (string.IsNullOrWhiteSpace(toCopy)) return;

            try
            {
                Clipboard.SetDataObject(toCopy);
                IsCopied = true;
                _ = Task.Delay(1500).ContinueWith(_ => UiNotify.RunOnUiAsync(() => IsCopied = false));
            }
            catch
            {
                // ignored
            }
        }

        private void OpenFileRepo()
        {
            if (string.IsNullOrWhiteSpace(DeptFileRepoPath)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = DeptFileRepoPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Warn("GroupHistoryItem", $"Failed to open file repo path '{DeptFileRepoPath}': {ex.Message}");
                UiNotify.Warn($"Could not open file repository: {ex.Message}");
            }
        }
    }
}
