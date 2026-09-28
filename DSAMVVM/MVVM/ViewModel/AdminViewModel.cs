using System.IO;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.Admin;
using DSAMVVM.MVVM.Model.Data;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public record AdminSectionOption(AdminSection Section, string DisplayName);

    public class AdminViewModel : ObservableObject, ISearchableViewModel
    {
        private const string Tag = "AdminVM";

        private readonly IAdminService _adminService;
        private readonly IJsonExportService _jsonExportService;
        private readonly IFileDialogService _fileDialogService;

        // Department tracking state
        private Department? _originalDept;
        private bool _isNewEntry;

        // Support Team tracking state
        private SupportTeam? _originalTeam;
        private bool _isNewTeam = true;

        // Links tracking state
        private Link? _originalLink;
        private bool _isNewLink = true;
        private bool _originalLinkIsCommon = true;
        private string? _originalLinkTeam;

        // ServiceMeow tracking state
        private ServiceMeowPet? _originalPet;
        private string? _originalPetOwnerNetId;
        private bool _isNewPet = true;

        // Section dropdown selection
        public IReadOnlyList<AdminSectionOption> AvailableSections { get; } =
        [
            new(AdminSection.Department, "Department"),
            new(AdminSection.SupportTeam, "Support Team"),
            new(AdminSection.Links, "Links"),
            new(AdminSection.ServiceMeow, "ServiceMeow")
        ];

        private AdminSectionOption? _selectedSectionOption;
        public AdminSectionOption? SelectedSectionOption
        {
            get => _selectedSectionOption ??= AvailableSections.FirstOrDefault(s => s.Section == _selectedSection);
            set
            {
                if (value == null || _selectedSectionOption == value) return;
                _selectedSectionOption = value;
                SelectedSection = value.Section;
                OnPropertyChanged(nameof(SelectedSectionOption));
            }
        }

        // Section selection
        private AdminSection _selectedSection = AdminSection.Department;
        public AdminSection SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (_selectedSection == value) return;
                _selectedSection = value;
                _selectedSectionOption = AvailableSections.FirstOrDefault(s => s.Section == value);
                OnPropertyChanged(nameof(SelectedSection));
                OnPropertyChanged(nameof(SelectedSectionOption));
                OnPropertyChanged(nameof(IsDepartmentSelected));
                OnPropertyChanged(nameof(IsSupportTeamSelected));
                OnPropertyChanged(nameof(IsLinksSelected));
                OnPropertyChanged(nameof(IsServiceMeowSelected));
                OnPropertyChanged(nameof(ShowDeleteSupportTeamButton));
                OnPropertyChanged(nameof(ShowDeleteLinkButton));
                OnPropertyChanged(nameof(ShowDeleteServiceMeowButton));
            }
        }

        public bool IsDepartmentSelected => SelectedSection == AdminSection.Department;
        public bool IsSupportTeamSelected => SelectedSection == AdminSection.SupportTeam;
        public bool IsLinksSelected => SelectedSection == AdminSection.Links;
        public bool IsServiceMeowSelected => SelectedSection == AdminSection.ServiceMeow;

        // Staging queue props
        public ObservableCollection<StagedChange> StagedChanges { get; } = [];
        public int PendingChangesCount => StagedChanges.Count;
        public bool HasPendingChanges => StagedChanges.Count > 0;

        #region Department Section Properties

        private string _deptId = string.Empty;
        public string DeptId
        {
            get => _deptId;
            set { _deptId = value; OnPropertyChanged(nameof(DeptId)); }
        }

        private bool _supportKnown = false;
        public bool SupportKnown
        {
            get => _supportKnown;
            set
            {
                if (_supportKnown == value) return;
                _supportKnown = value;
                OnPropertyChanged(nameof(SupportKnown));
                OnPropertyChanged(nameof(SelectedSupportKnownIndex));
            }
        }

        public int SelectedSupportKnownIndex
        {
            get => SupportKnown ? 0 : 1;
            set => SupportKnown = (value == 0);
        }

        public ObservableCollection<string> AvailableTeams { get; } = [];

        private string? _selectedTeamOption;
        public string? SelectedTeamOption
        {
            get => _selectedTeamOption;
            set
            {
                if (_selectedTeamOption == value) return;
                _selectedTeamOption = value;
                OnPropertyChanged(nameof(SelectedTeamOption));
                OnPropertyChanged(nameof(IsOtherTeamSelected));
                UpdateEffectiveDeptTeam();
            }
        }

        public bool IsOtherTeamSelected => string.Equals(SelectedTeamOption, "Other", StringComparison.OrdinalIgnoreCase);

        private string _customTeamName = string.Empty;
        public string CustomTeamName
        {
            get => _customTeamName;
            set
            {
                if (_customTeamName == value) return;
                _customTeamName = value;
                OnPropertyChanged(nameof(CustomTeamName));
                if (IsOtherTeamSelected)
                {
                    DeptTeam = value;
                }
            }
        }

        private string _deptTeam = string.Empty;
        public string DeptTeam
        {
            get => _deptTeam;
            set { _deptTeam = value; OnPropertyChanged(nameof(DeptTeam)); }
        }

        private string _deptNotes = string.Empty;
        public string DeptNotes
        {
            get => _deptNotes;
            set { _deptNotes = value; OnPropertyChanged(nameof(DeptNotes)); }
        }

        #endregion

        #region Support Team Section Properties

        public ObservableCollection<string> AvailableSupportTeamNames { get; } = [];

        private string? _selectedSupportTeamOption;
        public string? SelectedSupportTeamOption
        {
            get => _selectedSupportTeamOption;
            set
            {
                if (_selectedSupportTeamOption == value) return;
                _selectedSupportTeamOption = value;
                OnPropertyChanged(nameof(SelectedSupportTeamOption));
                _ = OnSupportTeamSelectionChangedAsync(value);
            }
        }

        private string _teamName = string.Empty;
        public string TeamName
        {
            get => _teamName;
            set
            {
                if (_teamName == value) return;
                _teamName = value;
                OnPropertyChanged(nameof(TeamName));
                OnPropertyChanged(nameof(CanDeleteTeam));
                OnPropertyChanged(nameof(ShowDeleteSupportTeamButton));
            }
        }

        private string _teamManagerName = string.Empty;
        public string TeamManagerName
        {
            get => _teamManagerName;
            set
            {
                if (_teamManagerName == value) return;
                _teamManagerName = value;
                OnPropertyChanged(nameof(TeamManagerName));
            }
        }

        private string _teamManagerNetId = string.Empty;
        public string TeamManagerNetId
        {
            get => _teamManagerNetId;
            set
            {
                if (_teamManagerNetId == value) return;
                _teamManagerNetId = value;
                OnPropertyChanged(nameof(TeamManagerNetId));
            }
        }

        private string _teamPhoneNumber = string.Empty;
        public string TeamPhoneNumber
        {
            get => _teamPhoneNumber;
            set
            {
                if (_teamPhoneNumber == value) return;
                _teamPhoneNumber = value;
                OnPropertyChanged(nameof(TeamPhoneNumber));
            }
        }

        public ObservableCollection<SupportedDivs> SupportedDivisionsList { get; } = [];

        private string _newDivAbbrev = string.Empty;
        public string NewDivAbbrev
        {
            get => _newDivAbbrev;
            set { _newDivAbbrev = value; OnPropertyChanged(nameof(NewDivAbbrev)); }
        }

        private string _newDivFullName = string.Empty;
        public string NewDivFullName
        {
            get => _newDivFullName;
            set { _newDivFullName = value; OnPropertyChanged(nameof(NewDivFullName)); }
        }

        public bool CanDeleteTeam => !_isNewTeam && !string.IsNullOrWhiteSpace(TeamName);
        public bool ShowDeleteSupportTeamButton => IsSupportTeamSelected && CanDeleteTeam;

        #endregion

        #region Links Section Properties

        private bool _isLinkCommon = true;
        public bool IsLinkCommon
        {
            get => _isLinkCommon;
            set
            {
                if (_isLinkCommon == value) return;
                _isLinkCommon = value;
                OnPropertyChanged(nameof(IsLinkCommon));
                OnPropertyChanged(nameof(IsLinkTeam));
                OnPropertyChanged(nameof(ShowDeleteLinkButton));
                _ = RefreshScopeLinksListAsync();
            }
        }

        public bool IsLinkTeam
        {
            get => !_isLinkCommon;
            set => IsLinkCommon = !value;
        }

        public ObservableCollection<string> AvailableLinkTeams { get; } = [];

        private string? _selectedLinkTeam;
        public string? SelectedLinkTeam
        {
            get => _selectedLinkTeam;
            set
            {
                if (_selectedLinkTeam == value) return;
                _selectedLinkTeam = value;
                OnPropertyChanged(nameof(SelectedLinkTeam));
                OnPropertyChanged(nameof(IsOtherLinkTeamSelected));
                _ = RefreshScopeLinksListAsync();
            }
        }

        public bool IsOtherLinkTeamSelected => string.Equals(SelectedLinkTeam, "Other", StringComparison.OrdinalIgnoreCase);

        private string _customLinkTeamName = string.Empty;
        public string CustomLinkTeamName
        {
            get => _customLinkTeamName;
            set
            {
                if (_customLinkTeamName == value) return;
                _customLinkTeamName = value;
                OnPropertyChanged(nameof(CustomLinkTeamName));
                _ = RefreshScopeLinksListAsync();
            }
        }

        public ObservableCollection<string> ScopeLinksList { get; } = [];

        private string? _selectedScopeLinkOption;
        public string? SelectedScopeLinkOption
        {
            get => _selectedScopeLinkOption;
            set
            {
                if (_selectedScopeLinkOption == value) return;
                _selectedScopeLinkOption = value;
                OnPropertyChanged(nameof(SelectedScopeLinkOption));
                _ = OnScopeLinkSelectionChangedAsync(value);
            }
        }

        private string _linkName = string.Empty;
        public string LinkName
        {
            get => _linkName;
            set
            {
                if (_linkName == value) return;
                _linkName = value;
                OnPropertyChanged(nameof(LinkName));
                OnPropertyChanged(nameof(CanDeleteLink));
                OnPropertyChanged(nameof(ShowDeleteLinkButton));
            }
        }

        private string _linkUrl = string.Empty;
        public string LinkUrl
        {
            get => _linkUrl;
            set
            {
                if (_linkUrl == value) return;
                _linkUrl = value;
                OnPropertyChanged(nameof(LinkUrl));
            }
        }

        private string _linkDescription = string.Empty;
        public string LinkDescription
        {
            get => _linkDescription;
            set
            {
                if (_linkDescription == value) return;
                _linkDescription = value;
                OnPropertyChanged(nameof(LinkDescription));
            }
        }

        public bool CanDeleteLink => !_isNewLink && !string.IsNullOrWhiteSpace(LinkName);
        public bool ShowDeleteLinkButton => IsLinksSelected && CanDeleteLink;

        #endregion

        #region ServiceMeow Section Properties

        public ObservableCollection<string> AvailablePetsList { get; } = [];

        private string? _selectedPetOption;
        public string? SelectedPetOption
        {
            get => _selectedPetOption;
            set
            {
                if (_selectedPetOption == value) return;
                _selectedPetOption = value;
                OnPropertyChanged(nameof(SelectedPetOption));
                _ = OnPetSelectionChangedAsync(value);
            }
        }

        // Owner fields
        private string _petOwnerNetId = string.Empty;
        public string PetOwnerNetId
        {
            get => _petOwnerNetId;
            set { _petOwnerNetId = value; OnPropertyChanged(nameof(PetOwnerNetId)); }
        }

        private string _petOwnerName = string.Empty;
        public string PetOwnerName
        {
            get => _petOwnerName;
            set { _petOwnerName = value; OnPropertyChanged(nameof(PetOwnerName)); }
        }

        private string _petOwnerTeam = string.Empty;
        public string PetOwnerTeam
        {
            get => _petOwnerTeam;
            set { _petOwnerTeam = value; OnPropertyChanged(nameof(PetOwnerTeam)); }
        }

        // Pet fields
        private string _petName = string.Empty;
        public string PetName
        {
            get => _petName;
            set
            {
                if (_petName == value) return;
                _petName = value;
                OnPropertyChanged(nameof(PetName));
                OnPropertyChanged(nameof(CanDeletePet));
                OnPropertyChanged(nameof(ShowDeleteServiceMeowButton));
            }
        }

        private string _petSpecies = "Cat";
        public string PetSpecies
        {
            get => _petSpecies;
            set { _petSpecies = value; OnPropertyChanged(nameof(PetSpecies)); }
        }

        public ObservableCollection<string> AvailableSpecies { get; } = ["Cat", "Dog", "Bird", "Reptile", "Fish", "Other"];

        private string _petBreed = string.Empty;
        public string PetBreed
        {
            get => _petBreed;
            set { _petBreed = value; OnPropertyChanged(nameof(PetBreed)); }
        }

        private string _petTitle = string.Empty;
        public string PetTitle
        {
            get => _petTitle;
            set { _petTitle = value; OnPropertyChanged(nameof(PetTitle)); }
        }

        private string _petBlurb = string.Empty;
        public string PetBlurb
        {
            get => _petBlurb;
            set { _petBlurb = value; OnPropertyChanged(nameof(PetBlurb)); }
        }

        private string _petImageUrl = string.Empty;
        public string PetImageUrl
        {
            get => _petImageUrl;
            set { _petImageUrl = value; OnPropertyChanged(nameof(PetImageUrl)); }
        }

        public bool CanDeletePet => !_isNewPet && !string.IsNullOrWhiteSpace(PetName);
        public bool ShowDeleteServiceMeowButton => IsServiceMeowSelected && CanDeletePet;

        #endregion

        // Commands
        public ICommand ApplyCommand { get; }
        public ICommand RemoveStagedItemCommand { get; }
        public ICommand DiscardAllCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand DeleteLinkCommand { get; }
        public ICommand DeleteSupportTeamCommand { get; }
        public ICommand DeleteServiceMeowCommand { get; }
        public ICommand AddDivisionCommand { get; }
        public ICommand RemoveDivisionCommand { get; }

        public AdminViewModel(IAdminService adminService, IJsonExportService jsonExportService, IFileDialogService fileDialogService)
        {
            _adminService = adminService ?? throw new ArgumentNullException(nameof(adminService));
            _jsonExportService = jsonExportService ?? throw new ArgumentNullException(nameof(jsonExportService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));

            ApplyCommand = new RelayCommand(_ => ExecuteApply());
            RemoveStagedItemCommand = new RelayCommand(param => ExecuteRemoveStagedItem(param));
            DiscardAllCommand = new RelayCommand(_ => ExecuteDiscardAll());
            ClearFormCommand = new RelayCommand(_ => ExecuteClearForm());
            SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());
            ExportJsonCommand = new RelayCommand(async _ => await ExecuteExportJsonAsync());
            DeleteLinkCommand = new RelayCommand(_ => StageLinkChange(isDelete: true));
            DeleteSupportTeamCommand = new RelayCommand(_ => StageSupportTeamChange(isDelete: true));
            DeleteServiceMeowCommand = new RelayCommand(_ => StageServiceMeowChange(isDelete: true));
            AddDivisionCommand = new RelayCommand(_ => ExecuteAddDivision());
            RemoveDivisionCommand = new RelayCommand(param => ExecuteRemoveDivision(param));

            _ = InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            await LoadAvailableTeamsAsync();
            await LoadAvailableSupportTeamsAsync();
            await LoadAvailableLinkTeamsAsync();
            await RefreshScopeLinksListAsync();
            await RefreshPetsListAsync();
        }

        #region Department Methods

        private async Task LoadAvailableTeamsAsync()
        {
            AvailableTeams.Clear();
            var teams = await _adminService.GetAvailableDepartmentTeamsAsync();
            foreach (var team in teams)
            {
                AvailableTeams.Add(team);
            }
            AvailableTeams.Add("Other");
        }

        private void UpdateEffectiveDeptTeam()
        {
            DeptTeam = IsOtherTeamSelected ? CustomTeamName.Trim() : (SelectedTeamOption ?? string.Empty);
        }

        private void SetTeamSelection(string? team)
        {
            if (string.IsNullOrWhiteSpace(team))
            {
                SelectedTeamOption = null;
                CustomTeamName = string.Empty;
                DeptTeam = string.Empty;
            }
            else
            {
                var match = AvailableTeams.FirstOrDefault(t =>
                    string.Equals(t, team.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(t, "Other", StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    SelectedTeamOption = match;
                    CustomTeamName = string.Empty;
                    DeptTeam = match;
                }
                else
                {
                    SelectedTeamOption = "Other";
                    CustomTeamName = team.Trim();
                    DeptTeam = team.Trim();
                }
            }
        }

        private void LoadDepartmentIntoForm(Department dept, bool isNew)
        {
            _isNewEntry = isNew;
            DeptId = dept.Number;
            SupportKnown = dept.SupportKnown;
            SetTeamSelection(dept.Team);
            DeptNotes = dept.Notes ?? string.Empty;
        }

        private void StageDepartmentChange()
        {
            UpdateEffectiveDeptTeam();
            var diff = new StringBuilder();

            if (_isNewEntry)
            {
                diff.Append("Created new Department");
            }
            else if (_originalDept != null)
            {
                if (_originalDept.SupportKnown != SupportKnown)
                    diff.Append($"SupportKnown: {_originalDept.SupportKnown} -> {SupportKnown}; ");
                if ((_originalDept.Team ?? string.Empty) != DeptTeam)
                    diff.Append($"Team: \"{_originalDept.Team}\" -> \"{DeptTeam}\"; ");
                if ((_originalDept.Notes ?? string.Empty) != DeptNotes)
                    diff.Append("Notes updated; ");

                if (diff.Length == 0)
                {
                    UiNotify.Info("No modifications detected to apply.", showStatusBar: true);
                    return;
                }
            }

            var updatedDept = new Department
            {
                Number = DeptId,
                SupportKnown = SupportKnown,
                Team = DeptTeam.Trim(),
                Notes = DeptNotes.Trim()
            };

            var existing = StagedChanges.FirstOrDefault(c => c.Section == AdminSection.Department && c.Key == DeptId);
            if (existing != null) StagedChanges.Remove(existing);

            StagedChanges.Add(new StagedChange
            {
                Section = AdminSection.Department,
                Key = DeptId,
                Summary = diff.ToString().TrimEnd(' ', ';'),
                StagedData = updatedDept
            });

            NotifyStagingChanged();
            UiNotify.Success($"Staged changes for Department {DeptId}. ({PendingChangesCount} pending)");
        }

        #endregion

        #region Support Team Methods

        private async Task LoadAvailableSupportTeamsAsync()
        {
            AvailableSupportTeamNames.Clear();
            AvailableSupportTeamNames.Add("[+ New Support Team]");

            var teams = await _adminService.LoadSupportTeamsAsync();
            foreach (var team in teams)
            {
                if (!string.IsNullOrWhiteSpace(team.SupportTeamName))
                    AvailableSupportTeamNames.Add(team.SupportTeamName);
            }

            if (string.IsNullOrWhiteSpace(SelectedSupportTeamOption) || !AvailableSupportTeamNames.Contains(SelectedSupportTeamOption))
            {
                _selectedSupportTeamOption = "[+ New Support Team]";
                OnPropertyChanged(nameof(SelectedSupportTeamOption));
            }
        }

        private async Task OnSupportTeamSelectionChangedAsync(string? selected)
        {
            if (string.IsNullOrWhiteSpace(selected) || selected == "[+ New Support Team]")
            {
                ResetSupportTeamForm();
                return;
            }

            var teams = await _adminService.LoadSupportTeamsAsync();
            var match = teams.FirstOrDefault(t =>
                string.Equals(t.SupportTeamName, selected, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                LoadSupportTeamIntoForm(match, isNew: false, syncSelector: false);
            }
        }

        private void LoadSupportTeamIntoForm(SupportTeam team, bool isNew, bool syncSelector = true)
        {
            _isNewTeam = isNew;
            _originalTeam = isNew ? null : new SupportTeam
            {
                SupportTeamName = team.SupportTeamName,
                ManagerName = team.ManagerName,
                ManagerNetID = team.ManagerNetID,
                PhoneNumber = team.PhoneNumber,
                SupportedDivisions = team.SupportedDivisions?.Select(d => new SupportedDivs { DivAbbrev = d.DivAbbrev, DivFullName = d.DivFullName }).ToList() ?? []
            };

            TeamName = team.SupportTeamName;
            TeamManagerName = team.ManagerName ?? string.Empty;
            TeamManagerNetId = team.ManagerNetID ?? string.Empty;
            TeamPhoneNumber = team.PhoneNumber ?? string.Empty;

            SupportedDivisionsList.Clear();
            if (team.SupportedDivisions != null)
            {
                foreach (var div in team.SupportedDivisions)
                {
                    SupportedDivisionsList.Add(new SupportedDivs { DivAbbrev = div.DivAbbrev, DivFullName = div.DivFullName });
                }
            }

            NewDivAbbrev = string.Empty;
            NewDivFullName = string.Empty;

            OnPropertyChanged(nameof(CanDeleteTeam));
            OnPropertyChanged(nameof(ShowDeleteSupportTeamButton));

            if (syncSelector)
            {
                _selectedSupportTeamOption = isNew ? "[+ New Support Team]" : team.SupportTeamName;
                OnPropertyChanged(nameof(SelectedSupportTeamOption));
            }
        }

        private void ResetSupportTeamForm()
        {
            _isNewTeam = true;
            _originalTeam = null;
            TeamName = string.Empty;
            TeamManagerName = string.Empty;
            TeamManagerNetId = string.Empty;
            TeamPhoneNumber = string.Empty;
            SupportedDivisionsList.Clear();
            NewDivAbbrev = string.Empty;
            NewDivFullName = string.Empty;

            OnPropertyChanged(nameof(CanDeleteTeam));
            OnPropertyChanged(nameof(ShowDeleteSupportTeamButton));
        }

        private void ExecuteAddDivision()
        {
            string abbrev = (NewDivAbbrev ?? string.Empty).Trim();
            string fullName = (NewDivFullName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(abbrev))
            {
                UiNotify.Warn("Please enter a Division Abbreviation.");
                return;
            }

            if (SupportedDivisionsList.Any(d => string.Equals(d.DivAbbrev, abbrev, StringComparison.OrdinalIgnoreCase)))
            {
                UiNotify.Warn($"Division '{abbrev}' is already added.");
                return;
            }

            SupportedDivisionsList.Add(new SupportedDivs { DivAbbrev = abbrev, DivFullName = fullName });
            NewDivAbbrev = string.Empty;
            NewDivFullName = string.Empty;
        }

        private void ExecuteRemoveDivision(object? param)
        {
            if (param is SupportedDivs div && SupportedDivisionsList.Contains(div))
            {
                SupportedDivisionsList.Remove(div);
            }
        }

        private void StageSupportTeamChange(bool isDelete = false)
        {
            string name = (TeamName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                UiNotify.Warn("Please enter a valid Support Team Name.");
                return;
            }

            var diff = new StringBuilder();

            if (isDelete)
            {
                diff.Append($"Delete Support Team '{name}'");
            }
            else if (_isNewTeam)
            {
                diff.Append($"Created new Support Team ({SupportedDivisionsList.Count} divisions)");
            }
            else if (_originalTeam != null)
            {
                if (_originalTeam.SupportTeamName != name)
                    diff.Append($"Name: \"{_originalTeam.SupportTeamName}\" -> \"{name}\"; ");
                if (_originalTeam.ManagerName != TeamManagerName.Trim())
                    diff.Append($"Manager: \"{_originalTeam.ManagerName}\" -> \"{TeamManagerName.Trim()}\"; ");
                if (_originalTeam.ManagerNetID != TeamManagerNetId.Trim())
                    diff.Append($"NetID: \"{_originalTeam.ManagerNetID}\" -> \"{TeamManagerNetId.Trim()}\"; ");
                if ((_originalTeam.PhoneNumber ?? string.Empty) != TeamPhoneNumber.Trim())
                    diff.Append("Phone updated; ");
                if ((_originalTeam.SupportedDivisions?.Count ?? 0) != SupportedDivisionsList.Count)
                    diff.Append($"Divisions: {_originalTeam.SupportedDivisions?.Count ?? 0} -> {SupportedDivisionsList.Count}; ");

                if (diff.Length == 0)
                {
                    UiNotify.Info("No modifications detected to apply.", showStatusBar: true);
                    return;
                }
            }

            var existing = StagedChanges.FirstOrDefault(c => c.Section == AdminSection.SupportTeam && c.Key == name);
            if (existing != null) StagedChanges.Remove(existing);

            var stagedTeam = new StagedSupportTeamData
            {
                Team = new SupportTeam
                {
                    SupportTeamName = name,
                    ManagerName = TeamManagerName.Trim(),
                    ManagerNetID = TeamManagerNetId.Trim(),
                    PhoneNumber = string.IsNullOrWhiteSpace(TeamPhoneNumber) ? null : TeamPhoneNumber.Trim(),
                    SupportedDivisions = SupportedDivisionsList.Select(d => new SupportedDivs { DivAbbrev = d.DivAbbrev.Trim(), DivFullName = d.DivFullName.Trim() }).ToList()
                },
                Action = isDelete ? StagedSupportTeamAction.Delete : StagedSupportTeamAction.AddOrUpdate
            };

            StagedChanges.Add(new StagedChange
            {
                Section = AdminSection.SupportTeam,
                Key = name,
                Summary = diff.ToString().TrimEnd(' ', ';'),
                StagedData = stagedTeam
            });

            NotifyStagingChanged();
            string actionVerb = isDelete ? "deletion of" : "changes for";
            UiNotify.Success($"Staged {actionVerb} team '{name}'. ({PendingChangesCount} pending)");

            ResetSupportTeamForm();
            _selectedSupportTeamOption = "[+ New Support Team]";
            OnPropertyChanged(nameof(SelectedSupportTeamOption));
        }

        #endregion

        #region Links Methods

        private string GetEffectiveLinkTeam()
        {
            if (IsOtherLinkTeamSelected) return CustomLinkTeamName.Trim();
            return (SelectedLinkTeam ?? string.Empty).Trim();
        }

        private async Task LoadAvailableLinkTeamsAsync()
        {
            AvailableLinkTeams.Clear();
            var teams = await _adminService.GetAvailableLinkTeamsAsync();
            foreach (var team in teams)
            {
                AvailableLinkTeams.Add(team);
            }
            AvailableLinkTeams.Add("Other");

            if (SelectedLinkTeam == null && AvailableLinkTeams.Count > 1)
            {
                SelectedLinkTeam = AvailableLinkTeams[0];
            }
        }

        private async Task RefreshScopeLinksListAsync()
        {
            ScopeLinksList.Clear();
            ScopeLinksList.Add("[+ New Link]");

            var data = await _adminService.LoadLinksDataAsync();

            if (IsLinkCommon)
            {
                foreach (var link in data.CommonLinks.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(link.Name))
                        ScopeLinksList.Add(link.Name);
                }
            }
            else
            {
                string effectiveTeam = GetEffectiveLinkTeam();
                if (!string.IsNullOrWhiteSpace(effectiveTeam))
                {
                    var group = data.TeamLinks.FirstOrDefault(g =>
                        string.Equals(g.Team, effectiveTeam, StringComparison.OrdinalIgnoreCase));

                    if (group != null)
                    {
                        foreach (var link in group.Links.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(link.Name))
                                ScopeLinksList.Add(link.Name);
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(SelectedScopeLinkOption) || !ScopeLinksList.Contains(SelectedScopeLinkOption))
            {
                _selectedScopeLinkOption = "[+ New Link]";
                OnPropertyChanged(nameof(SelectedScopeLinkOption));
            }
        }

        private async Task OnScopeLinkSelectionChangedAsync(string? selected)
        {
            if (string.IsNullOrWhiteSpace(selected) || selected == "[+ New Link]")
            {
                LinkName = string.Empty;
                LinkUrl = string.Empty;
                LinkDescription = string.Empty;
                _originalLink = null;
                _isNewLink = true;
                OnPropertyChanged(nameof(CanDeleteLink));
                OnPropertyChanged(nameof(ShowDeleteLinkButton));
                return;
            }

            var data = await _adminService.LoadLinksDataAsync();
            if (IsLinkCommon)
            {
                var match = data.CommonLinks.FirstOrDefault(l =>
                    string.Equals(l.Name, selected, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    LoadLinkIntoForm(match, isCommon: true, team: null, isNew: false, syncSelector: false);
                }
            }
            else
            {
                string team = GetEffectiveLinkTeam();
                var group = data.TeamLinks.FirstOrDefault(g =>
                    string.Equals(g.Team, team, StringComparison.OrdinalIgnoreCase));

                var match = group?.Links.FirstOrDefault(l =>
                    string.Equals(l.Name, selected, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    LoadLinkIntoForm(match, isCommon: false, team: team, isNew: false, syncSelector: false);
                }
            }
        }

        private void LoadLinkIntoForm(Link link, bool isCommon, string? team, bool isNew, bool syncSelector = true)
        {
            _isNewLink = isNew;
            _originalLink = isNew ? null : new Link
            {
                Name = link.Name,
                URL = link.URL,
                Description = link.Description
            };
            _originalLinkIsCommon = isCommon;
            _originalLinkTeam = team;

            _isLinkCommon = isCommon;
            OnPropertyChanged(nameof(IsLinkCommon));
            OnPropertyChanged(nameof(IsLinkTeam));

            if (!isCommon && !string.IsNullOrWhiteSpace(team))
            {
                var match = AvailableLinkTeams.FirstOrDefault(t =>
                    string.Equals(t, team.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(t, "Other", StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    SelectedLinkTeam = match;
                    CustomLinkTeamName = string.Empty;
                }
                else
                {
                    SelectedLinkTeam = "Other";
                    CustomLinkTeamName = team.Trim();
                }
            }

            LinkName = link.Name;
            LinkUrl = link.URL;
            LinkDescription = link.Description ?? string.Empty;

            OnPropertyChanged(nameof(CanDeleteLink));
            OnPropertyChanged(nameof(ShowDeleteLinkButton));

            _ = RefreshScopeLinksListAsync();

            if (syncSelector)
            {
                _selectedScopeLinkOption = isNew ? "[+ New Link]" : link.Name;
                OnPropertyChanged(nameof(SelectedScopeLinkOption));
            }
        }

        private void StageLinkChange(bool isDelete = false)
        {
            string name = (LinkName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                UiNotify.Warn("Please enter a valid Link Name.");
                return;
            }

            string effectiveTeam = GetEffectiveLinkTeam();
            if (!IsLinkCommon && string.IsNullOrWhiteSpace(effectiveTeam))
            {
                UiNotify.Warn("Please specify a Support Team for this Team Link.");
                return;
            }

            string url = (LinkUrl ?? string.Empty).Trim();
            if (!isDelete && string.IsNullOrWhiteSpace(url))
            {
                UiNotify.Warn("Please enter a valid URL for the link.");
                return;
            }

            string description = (LinkDescription ?? string.Empty).Trim();
            string stagedKey = IsLinkCommon ? name : $"[{effectiveTeam}] {name}";
            var diff = new StringBuilder();

            if (isDelete)
            {
                diff.Append(IsLinkCommon ? "Delete from Common Links" : $"Delete from [{effectiveTeam}]");
            }
            else if (_isNewLink)
            {
                diff.Append(IsLinkCommon ? "Created new Common Link" : $"Created new Link in [{effectiveTeam}]");
            }
            else if (_originalLink != null)
            {
                if (_originalLink.Name != name)
                    diff.Append($"Name: \"{_originalLink.Name}\" -> \"{name}\"; ");
                if (_originalLink.URL != url)
                    diff.Append($"URL: \"{_originalLink.URL}\" -> \"{url}\"; ");
                if ((_originalLink.Description ?? string.Empty) != description)
                    diff.Append("Description updated; ");
                if (_originalLinkIsCommon != IsLinkCommon || (!IsLinkCommon && _originalLinkTeam != effectiveTeam))
                    diff.Append("Scope/Team reassigned; ");

                if (diff.Length == 0)
                {
                    UiNotify.Info("No modifications detected to apply.", showStatusBar: true);
                    return;
                }
            }

            var existing = StagedChanges.FirstOrDefault(c => c.Section == AdminSection.Links && c.Key == stagedKey);
            if (existing != null) StagedChanges.Remove(existing);

            var stagedLinkData = new StagedLinkData
            {
                IsCommon = IsLinkCommon,
                Team = IsLinkCommon ? null : effectiveTeam,
                Link = new Link
                {
                    Name = name,
                    URL = url,
                    Description = description
                },
                Action = isDelete ? StagedLinkAction.Delete : StagedLinkAction.AddOrUpdate
            };

            StagedChanges.Add(new StagedChange
            {
                Section = AdminSection.Links,
                Key = stagedKey,
                Summary = diff.ToString().TrimEnd(' ', ';'),
                StagedData = stagedLinkData
            });

            NotifyStagingChanged();
            string actionVerb = isDelete ? "deletion of" : "changes for";
            UiNotify.Success($"Staged {actionVerb} link '{name}'. ({PendingChangesCount} pending)");

            // Reset form to ready state
            _isNewLink = true;
            _originalLink = null;
            LinkName = string.Empty;
            LinkUrl = string.Empty;
            LinkDescription = string.Empty;
            _selectedScopeLinkOption = "[+ New Link]";
            OnPropertyChanged(nameof(SelectedScopeLinkOption));
            OnPropertyChanged(nameof(CanDeleteLink));
            OnPropertyChanged(nameof(ShowDeleteLinkButton));
        }

        #endregion

        #region ServiceMeow Methods

        private async Task RefreshPetsListAsync()
        {
            AvailablePetsList.Clear();
            AvailablePetsList.Add("[+ New Pet]");

            var data = await _adminService.LoadServiceMeowDataAsync();
            foreach (var pet in data.AllPets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(pet.Name))
                {
                    string label = string.IsNullOrWhiteSpace(pet.Owner?.NetId)
                        ? pet.Name
                        : $"{pet.Name} ({pet.Owner.NetId})";
                    AvailablePetsList.Add(label);
                }
            }

            if (string.IsNullOrWhiteSpace(SelectedPetOption) || !AvailablePetsList.Contains(SelectedPetOption))
            {
                _selectedPetOption = "[+ New Pet]";
                OnPropertyChanged(nameof(SelectedPetOption));
            }
        }

        private async Task OnPetSelectionChangedAsync(string? selected)
        {
            if (string.IsNullOrWhiteSpace(selected) || selected == "[+ New Pet]")
            {
                ResetPetForm();
                return;
            }

            var data = await _adminService.LoadServiceMeowDataAsync();
            var match = data.AllPets.FirstOrDefault(p =>
            {
                string label = string.IsNullOrWhiteSpace(p.Owner?.NetId)
                    ? p.Name
                    : $"{p.Name} ({p.Owner.NetId})";
                return string.Equals(label, selected, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(p.Name, selected, StringComparison.OrdinalIgnoreCase);
            });

            if (match != null)
            {
                LoadPetIntoForm(match, isNew: false, syncSelector: false);
            }
        }

        private void LoadPetIntoForm(ServiceMeowPet pet, bool isNew, bool syncSelector = true)
        {
            _isNewPet = isNew;
            _originalPet = isNew ? null : new ServiceMeowPet
            {
                Id = pet.Id,
                Name = pet.Name,
                Species = pet.Species,
                Breed = pet.Breed,
                Title = pet.Title,
                Blurb = pet.Blurb,
                Images = [.. pet.Images]
            };
            _originalPetOwnerNetId = pet.Owner?.NetId;

            PetOwnerNetId = pet.Owner?.NetId ?? string.Empty;
            PetOwnerName = pet.Owner?.Name ?? string.Empty;
            PetOwnerTeam = pet.Owner?.Team ?? string.Empty;

            PetName = pet.Name;
            PetSpecies = string.IsNullOrWhiteSpace(pet.Species) ? "Cat" : pet.Species;
            PetBreed = pet.Breed ?? string.Empty;
            PetTitle = pet.Title ?? string.Empty;
            PetBlurb = pet.Blurb ?? string.Empty;
            PetImageUrl = pet.Images.Count > 0 ? string.Join(", ", pet.Images) : string.Empty;

            OnPropertyChanged(nameof(CanDeletePet));
            OnPropertyChanged(nameof(ShowDeleteServiceMeowButton));

            if (syncSelector)
            {
                string label = string.IsNullOrWhiteSpace(pet.Owner?.NetId)
                    ? pet.Name
                    : $"{pet.Name} ({pet.Owner.NetId})";
                _selectedPetOption = isNew ? "[+ New Pet]" : label;
                OnPropertyChanged(nameof(SelectedPetOption));
            }
        }

        private void ResetPetForm()
        {
            _isNewPet = true;
            _originalPet = null;
            _originalPetOwnerNetId = null;

            PetOwnerNetId = string.Empty;
            PetOwnerName = string.Empty;
            PetOwnerTeam = string.Empty;

            PetName = string.Empty;
            PetSpecies = "Cat";
            PetBreed = string.Empty;
            PetTitle = string.Empty;
            PetBlurb = string.Empty;
            PetImageUrl = string.Empty;

            OnPropertyChanged(nameof(CanDeletePet));
            OnPropertyChanged(nameof(ShowDeleteServiceMeowButton));
        }

        private void StageServiceMeowChange(bool isDelete = false)
        {
            string petName = (PetName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(petName))
            {
                UiNotify.Warn("Please enter a valid Pet Name.");
                return;
            }

            string ownerNetId = (PetOwnerNetId ?? string.Empty).Trim();
            if (!isDelete && string.IsNullOrWhiteSpace(ownerNetId))
            {
                UiNotify.Warn("Please enter an Owner NetID for this pet.");
                return;
            }

            string ownerName = (PetOwnerName ?? string.Empty).Trim();
            string ownerTeam = (PetOwnerTeam ?? string.Empty).Trim();
            string species = string.IsNullOrWhiteSpace(PetSpecies) ? "Cat" : PetSpecies.Trim();
            string breed = (PetBreed ?? string.Empty).Trim();
            string title = (PetTitle ?? string.Empty).Trim();
            string blurb = (PetBlurb ?? string.Empty).Trim();
            string stagedKey = string.IsNullOrWhiteSpace(ownerNetId) ? petName : $"{petName} ({ownerNetId})";

            var diff = new StringBuilder();

            if (isDelete)
            {
                diff.Append($"Delete pet '{petName}' ({ownerNetId})");
            }
            else if (_isNewPet)
            {
                diff.Append($"Created new pet '{petName}' for {ownerName} ({ownerNetId})");
            }
            else if (_originalPet != null)
            {
                if (_originalPet.Name != petName)
                    diff.Append($"Name: \"{_originalPet.Name}\" -> \"{petName}\"; ");
                if (_originalPet.Species != species)
                    diff.Append($"Species: \"{_originalPet.Species}\" -> \"{species}\"; ");
                if ((_originalPet.Breed ?? string.Empty) != breed)
                    diff.Append($"Breed: \"{_originalPet.Breed}\" -> \"{breed}\"; ");
                if ((_originalPet.Title ?? string.Empty) != title)
                    diff.Append($"Title: \"{_originalPet.Title}\" -> \"{title}\"; ");
                if ((_originalPet.Blurb ?? string.Empty) != blurb)
                    diff.Append("Blurb updated; ");

                string origImages = string.Join(", ", _originalPet.Images);
                if (origImages != (PetImageUrl ?? string.Empty).Trim())
                    diff.Append("Photo URL updated; ");

                if ((_originalPetOwnerNetId ?? string.Empty) != ownerNetId)
                    diff.Append($"Owner NetID: \"{_originalPetOwnerNetId}\" -> \"{ownerNetId}\"; ");

                if (diff.Length == 0)
                {
                    UiNotify.Info("No modifications detected to apply.", showStatusBar: true);
                    return;
                }
            }

            var imageList = (PetImageUrl ?? string.Empty)
                .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var petObj = new ServiceMeowPet
            {
                Id = _originalPet?.Id ?? Guid.NewGuid().ToString("N")[..8],
                Name = petName,
                Species = species,
                Breed = breed,
                Title = title,
                Blurb = blurb,
                Images = imageList
            };

            var stagedMeowData = new StagedServiceMeowData
            {
                OriginalOwnerNetId = _originalPetOwnerNetId,
                OwnerNetId = ownerNetId,
                OwnerName = ownerName,
                OwnerTeam = ownerTeam,
                Pet = petObj,
                Action = isDelete ? StagedServiceMeowAction.Delete : StagedServiceMeowAction.AddOrUpdate
            };

            // Remove previous staged change for this pet
            var existing = StagedChanges.FirstOrDefault(c =>
                c.Section == AdminSection.ServiceMeow &&
                (c.Key == stagedKey ||
                 (_originalPetOwnerNetId != null && c.Key == $"{petName} ({_originalPetOwnerNetId})") ||
                 (c.StagedData is StagedServiceMeowData sd && sd.Pet.Id == petObj.Id)));

            if (existing != null) StagedChanges.Remove(existing);

            StagedChanges.Add(new StagedChange
            {
                Section = AdminSection.ServiceMeow,
                Key = stagedKey,
                Summary = diff.ToString().TrimEnd(' ', ';'),
                StagedData = stagedMeowData
            });

            NotifyStagingChanged();
            string actionVerb = isDelete ? "deletion of" : "changes for";
            UiNotify.Success($"Staged {actionVerb} pet '{petName}'. ({PendingChangesCount} pending)");

            ResetPetForm();
            _selectedPetOption = "[+ New Pet]";
            OnPropertyChanged(nameof(SelectedPetOption));
        }

        #endregion

        private void ExecuteClearForm()
        {
            if (SelectedSection == AdminSection.Department)
            {
                DeptId = string.Empty;
                SupportKnown = false;
                SetTeamSelection(null);
                DeptNotes = string.Empty;
                _originalDept = null;
                _isNewEntry = false;
            }
            else if (SelectedSection == AdminSection.SupportTeam)
            {
                ResetSupportTeamForm();
                _selectedSupportTeamOption = "[+ New Support Team]";
                OnPropertyChanged(nameof(SelectedSupportTeamOption));
            }
            else if (SelectedSection == AdminSection.Links)
            {
                LinkName = string.Empty;
                LinkUrl = string.Empty;
                LinkDescription = string.Empty;
                _originalLink = null;
                _isNewLink = true;
                _selectedScopeLinkOption = "[+ New Link]";
                OnPropertyChanged(nameof(SelectedScopeLinkOption));
                OnPropertyChanged(nameof(CanDeleteLink));
                OnPropertyChanged(nameof(ShowDeleteLinkButton));
            }
            else if (SelectedSection == AdminSection.ServiceMeow)
            {
                ResetPetForm();
                _selectedPetOption = "[+ New Pet]";
                OnPropertyChanged(nameof(SelectedPetOption));
            }
        }

        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            var query = (context.Query ?? string.Empty).Trim();

            switch (SelectedSection)
            {
                case AdminSection.Department:
                    if (query.Length != 4 || !int.TryParse(query, out _))
                    {
                        UiNotify.Warn("Please enter a valid 4-digit Department ID.");
                        return;
                    }

                    var existingStaged = StagedChanges.FirstOrDefault(c => c.Section == AdminSection.Department && c.Key == query);
                    if (existingStaged?.StagedData is Department stagedDept)
                    {
                        LoadDepartmentIntoForm(stagedDept, isNew: false);
                        UiNotify.Info($"Loaded Department {query} from pending staged changes.", showStatusBar: true);
                        return;
                    }

                    var dept = await _adminService.FindDepartmentAsync(query);
                    if (dept != null)
                    {
                        _originalDept = new Department
                        {
                            Number = dept.Number,
                            SupportKnown = dept.SupportKnown,
                            Team = dept.Team,
                            Notes = dept.Notes,
                            FileRepoPath = dept.FileRepoPath
                        };

                        LoadDepartmentIntoForm(dept, isNew: false);
                        UiNotify.Success($"Loaded Department {dept.Number}.");
                    }
                    else
                    {
                        _originalDept = null;
                        _isNewEntry = true;

                        DeptId = query;
                        SupportKnown = false;
                        SetTeamSelection(null);
                        DeptNotes = string.Empty;

                        UiNotify.Info($"Department {query} not found. Ready to create.", showStatusBar: true);
                    }
                    break;

                case AdminSection.SupportTeam:
                    if (string.IsNullOrWhiteSpace(query)) return;

                    var existingStagedTeam = StagedChanges.FirstOrDefault(c =>
                        c.Section == AdminSection.SupportTeam &&
                        c.Key.Equals(query, StringComparison.OrdinalIgnoreCase));

                    if (existingStagedTeam?.StagedData is StagedSupportTeamData sstd)
                    {
                        LoadSupportTeamIntoForm(sstd.Team, isNew: false);
                        UiNotify.Info($"Loaded Support Team '{sstd.Team.SupportTeamName}' from pending staged changes.", showStatusBar: true);
                        return;
                    }

                    var foundTeam = await _adminService.FindSupportTeamAsync(query);
                    if (foundTeam != null)
                    {
                        LoadSupportTeamIntoForm(foundTeam, isNew: false);
                        UiNotify.Success($"Loaded Support Team '{foundTeam.SupportTeamName}'.");
                    }
                    else
                    {
                        ResetSupportTeamForm();
                        TeamName = query;
                        _selectedSupportTeamOption = "[+ New Support Team]";
                        OnPropertyChanged(nameof(SelectedSupportTeamOption));

                        UiNotify.Info($"Support Team '{query}' not found. Ready to create.", showStatusBar: true);
                    }
                    break;

                case AdminSection.Links:
                    if (string.IsNullOrWhiteSpace(query)) return;

                    var existingStagedLink = StagedChanges.FirstOrDefault(c =>
                        c.Section == AdminSection.Links &&
                        (c.Key.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                         (c.StagedData is StagedLinkData sld && sld.Link.Name.Equals(query, StringComparison.OrdinalIgnoreCase))));

                    if (existingStagedLink?.StagedData is StagedLinkData stagedLink)
                    {
                        LoadLinkIntoForm(stagedLink.Link, stagedLink.IsCommon, stagedLink.Team, isNew: false);
                        UiNotify.Info($"Loaded link '{stagedLink.Link.Name}' from pending staged changes.", showStatusBar: true);
                        return;
                    }

                    var (foundLink, isCommon, teamName) = await _adminService.FindLinkAsync(query);
                    if (foundLink != null)
                    {
                        LoadLinkIntoForm(foundLink, isCommon, teamName, isNew: false);
                        string scopeDesc = isCommon ? "Common Link" : $"Team Link for [{teamName}]";
                        UiNotify.Success($"Loaded {scopeDesc} '{foundLink.Name}'.");
                    }
                    else
                    {
                        _originalLink = null;
                        _isNewLink = true;
                        LinkName = query;
                        LinkUrl = string.Empty;
                        LinkDescription = string.Empty;
                        _selectedScopeLinkOption = "[+ New Link]";
                        OnPropertyChanged(nameof(SelectedScopeLinkOption));
                        OnPropertyChanged(nameof(CanDeleteLink));
                        OnPropertyChanged(nameof(ShowDeleteLinkButton));

                        UiNotify.Info($"Link '{query}' not found. Ready to create.", showStatusBar: true);
                    }
                    break;

                case AdminSection.ServiceMeow:
                    if (string.IsNullOrWhiteSpace(query)) return;

                    var existingStagedMeow = StagedChanges.FirstOrDefault(c =>
                        c.Section == AdminSection.ServiceMeow &&
                        (c.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         (c.StagedData is StagedServiceMeowData md &&
                          (md.Pet.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           md.OwnerNetId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           md.OwnerName.Contains(query, StringComparison.OrdinalIgnoreCase)))));

                    if (existingStagedMeow?.StagedData is StagedServiceMeowData stagedMeow)
                    {
                        var petToLoad = stagedMeow.Pet;
                        petToLoad.Owner = new ServiceMeowOwner
                        {
                            NetId = stagedMeow.OwnerNetId,
                            Name = stagedMeow.OwnerName,
                            Team = stagedMeow.OwnerTeam
                        };
                        LoadPetIntoForm(petToLoad, isNew: false);
                        UiNotify.Info($"Loaded pet '{petToLoad.Name}' from pending staged changes.", showStatusBar: true);
                        return;
                    }

                    var foundPet = await _adminService.FindPetAsync(query);
                    if (foundPet != null)
                    {
                        LoadPetIntoForm(foundPet, isNew: false);
                        string ownerDesc = foundPet.Owner != null ? $" ({foundPet.Owner.Name})" : string.Empty;
                        UiNotify.Success($"Loaded pet '{foundPet.Name}'{ownerDesc}.");
                    }
                    else
                    {
                        ResetPetForm();
                        PetName = query;
                        _selectedPetOption = "[+ New Pet]";
                        OnPropertyChanged(nameof(SelectedPetOption));

                        UiNotify.Info($"Pet '{query}' not found. Ready to create.", showStatusBar: true);
                    }
                    break;
            }
        }

        private void ExecuteApply()
        {
            switch (SelectedSection)
            {
                case AdminSection.Department:
                    if (string.IsNullOrWhiteSpace(DeptId))
                    {
                        UiNotify.Warn("No department entity loaded to apply changes to.");
                        return;
                    }
                    StageDepartmentChange();
                    break;

                case AdminSection.SupportTeam:
                    StageSupportTeamChange(isDelete: false);
                    break;

                case AdminSection.Links:
                    StageLinkChange(isDelete: false);
                    break;

                case AdminSection.ServiceMeow:
                    StageServiceMeowChange(isDelete: false);
                    break;
            }
        }

        private void ExecuteRemoveStagedItem(object? param)
        {
            if (param is StagedChange change && StagedChanges.Contains(change))
            {
                StagedChanges.Remove(change);
                NotifyStagingChanged();
                UiNotify.Info($"Removed {change.Key} from queue.", showStatusBar: true);
            }
        }

        private void ExecuteDiscardAll()
        {
            if (!HasPendingChanges) return;

            StagedChanges.Clear();
            NotifyStagingChanged();
            UiNotify.Info("All pending staged changes have been discarded.", showStatusBar: true);
        }

        private async Task ExecuteExportJsonAsync()
        {
            try
            {
                bool hasDept = StagedChanges.Any(c => c.Section == AdminSection.Department || c.Section == AdminSection.SupportTeam);
                bool hasLinks = StagedChanges.Any(c => c.Section == AdminSection.Links);
                bool hasMeow = StagedChanges.Any(c => c.Section == AdminSection.ServiceMeow);

                int stagedTypes = (hasDept ? 1 : 0) + (hasLinks ? 1 : 0) + (hasMeow ? 1 : 0);

                // Case 1: Multiple sections staged -> prompt for folder
                if (stagedTypes > 1)
                {
                    var folder = _fileDialogService.SelectFolder("Select destination folder to export JSON files for Box");
                    if (string.IsNullOrWhiteSpace(folder)) return;

                    var exportedFiles = await _jsonExportService.ExportStagedChangesToFolderAsync(StagedChanges, folder);
                    UiNotify.Success($"Exported {exportedFiles.Count} JSON file(s) to {folder} for Box upload!");
                    return;
                }

                // Case 2: Only ServiceMeow staged -> prompt for servicemeow.json save location
                if (hasMeow)
                {
                    var savePath = _fileDialogService.SaveFile("servicemeow.json", "JSON Data (*.json)|*.json", "Export servicemeow.json for Box");
                    if (string.IsNullOrWhiteSpace(savePath)) return;

                    await _jsonExportService.ExportServiceMeowJsonAsync(StagedChanges, savePath);
                    UiNotify.Success($"Exported servicemeow.json to {Path.GetFileName(savePath)} for Box upload!");
                    return;
                }

                // Case 3: Only Links staged -> prompt for links.json save location
                if (hasLinks)
                {
                    var savePath = _fileDialogService.SaveFile("links.json", "JSON Data (*.json)|*.json", "Export links.json for Box");
                    if (string.IsNullOrWhiteSpace(savePath)) return;

                    await _jsonExportService.ExportLinksJsonAsync(StagedChanges, savePath);
                    UiNotify.Success($"Exported links.json to {Path.GetFileName(savePath)} for Box upload!");
                    return;
                }

                // Case 4: Only Department/Team staged -> prompt for departments.json save location
                if (hasDept)
                {
                    var savePath = _fileDialogService.SaveFile("departments.json", "JSON Data (*.json)|*.json", "Export departments.json for Box");
                    if (string.IsNullOrWhiteSpace(savePath)) return;

                    await _jsonExportService.ExportDepartmentsJsonAsync(StagedChanges, savePath);
                    UiNotify.Success($"Exported departments.json to {Path.GetFileName(savePath)} for Box upload!");
                    return;
                }

                // Case 5: No staged changes -> export baseline for currently selected section
                string defaultName = SelectedSection switch
                {
                    AdminSection.Links => "links.json",
                    AdminSection.ServiceMeow => "servicemeow.json",
                    _ => "departments.json"
                };
                var baselinePath = _fileDialogService.SaveFile(defaultName, "JSON Data (*.json)|*.json", $"Export {defaultName} for Box");
                if (string.IsNullOrWhiteSpace(baselinePath)) return;

                if (SelectedSection == AdminSection.Links)
                {
                    await _jsonExportService.ExportLinksJsonAsync(null, baselinePath);
                }
                else if (SelectedSection == AdminSection.ServiceMeow)
                {
                    await _jsonExportService.ExportServiceMeowJsonAsync(null, baselinePath);
                }
                else
                {
                    await _jsonExportService.ExportDepartmentsJsonAsync(null, baselinePath);
                }

                UiNotify.Success($"Exported {defaultName} to {Path.GetFileName(baselinePath)} for Box upload!");
            }
            catch (Exception ex)
            {
                Log.Error(Tag, "Failed to export JSON.", ex);
                UiNotify.Error("Export Failed", ex.Message, ex);
            }
        }

        private async Task ExecuteSaveAsync()
        {
            if (!HasPendingChanges)
            {
                UiNotify.Warn("No staged changes to save.");
                return;
            }

            int deptCount = StagedChanges.Count(c => c.Section == AdminSection.Department);
            int teamCount = StagedChanges.Count(c => c.Section == AdminSection.SupportTeam);
            int linkCount = StagedChanges.Count(c => c.Section == AdminSection.Links);
            int petCount = StagedChanges.Count(c => c.Section == AdminSection.ServiceMeow);

            try
            {
                await _adminService.SaveStagedChangesAsync(StagedChanges);

                StagedChanges.Clear();
                NotifyStagingChanged();

                await LoadAvailableTeamsAsync();
                await LoadAvailableSupportTeamsAsync();
                await LoadAvailableLinkTeamsAsync();
                await RefreshScopeLinksListAsync();
                await RefreshPetsListAsync();

                var report = new List<string>();
                if (deptCount > 0 || teamCount > 0)
                {
                    var deptParts = new List<string>();
                    if (deptCount > 0) deptParts.Add($"{deptCount} department(s)");
                    if (teamCount > 0) deptParts.Add($"{teamCount} support team(s)");
                    report.Add($"{string.Join(" and ", deptParts)} to departments.json");
                }
                if (linkCount > 0) report.Add($"{linkCount} link(s) to links.json");
                if (petCount > 0) report.Add($"{petCount} pet(s) to servicemeow.json");
                UiNotify.Success($"Saved {string.Join(" and ", report)}!");
            }
            catch (Exception ex)
            {
                Log.Error(Tag, "Failed to save staged changes.", ex);
                UiNotify.Error("Failed to save JSON", ex.Message, ex);
            }
        }

        private void NotifyStagingChanged()
        {
            OnPropertyChanged(nameof(PendingChangesCount));
            OnPropertyChanged(nameof(HasPendingChanges));
        }
    }
}
