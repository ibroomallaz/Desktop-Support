using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Admin;
using DSAMVVM.MVVM.Model.Data;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public class AdminViewModel : ObservableObject, ISearchableViewModel
    {
        private readonly IDepartmentService? _deptService;
        private Department? _originalDept;
        private bool _isNewEntry;

        //Section selection
        private AdminSection _selectedSection = AdminSection.Department;
        public AdminSection SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (_selectedSection == value) return;
                _selectedSection = value;
                OnPropertyChanged(nameof(SelectedSection));
                OnPropertyChanged(nameof(IsDepartmentSelected));
                OnPropertyChanged(nameof(IsSupportTeamSelected));
                OnPropertyChanged(nameof(IsLinksSelected));
            }
        }

        public bool IsDepartmentSelected => SelectedSection == AdminSection.Department;
        public bool IsSupportTeamSelected => SelectedSection == AdminSection.SupportTeam;
        public bool IsLinksSelected => SelectedSection == AdminSection.Links;

        // Staging queue props
        public ObservableCollection<StagedChange> StagedChanges { get; } = [];
        public int PendingChangesCount => StagedChanges.Count;
        public bool HasPendingChanges => StagedChanges.Count > 0;

        // Dept props
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

        // Teams list loaded from JSON SupportTeams array + "Other"
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

        // Effective team string committed to the model
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

        // COMMANDS
        public ICommand ApplyCommand { get; }
        public ICommand RemoveStagedItemCommand { get; }
        public ICommand DiscardAllCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand SaveCommand { get; }

        // CTOR
        public AdminViewModel(IDepartmentService? deptService = null)
        {
            _deptService = deptService;

            ApplyCommand = new RelayCommand(_ => ExecuteApply());
            RemoveStagedItemCommand = new RelayCommand(param => ExecuteRemoveStagedItem(param));
            DiscardAllCommand = new RelayCommand(_ => ExecuteDiscardAll());
            ClearFormCommand = new RelayCommand(_ => ExecuteClearForm());
            SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());

            LoadAvailableTeams();
        }

        // Reads the SupportTeams array from JSON and populates the ComboBox
        private void LoadAvailableTeams()
        {
            AvailableTeams.Clear();
            var teamSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "departments.json");
                if (!File.Exists(targetPath) && File.Exists(Globals.g_DepartmentCachePath))
                {
                    targetPath = Globals.g_DepartmentCachePath;
                }

                if (File.Exists(targetPath))
                {
                    string json = File.ReadAllText(targetPath);
                    var wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(json);

                    // Pull names from SupportTeams list
                    if (wrapper?.SupportTeams != null)
                    {
                        foreach (var st in wrapper.SupportTeams)
                        {
                            if (!string.IsNullOrWhiteSpace(st.SupportTeamName))
                                teamSet.Add(st.SupportTeamName.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Admin", $"Could not pre-load support teams: {ex.Message}");
            }

            foreach (var team in teamSet.OrderBy(t => t))
            {
                AvailableTeams.Add(team);
            }

            // Append "Other" for custom entries
            AvailableTeams.Add("Other");
        }

        private void UpdateEffectiveDeptTeam()
        {
            if (IsOtherTeamSelected)
            {
                DeptTeam = CustomTeamName.Trim();
            }
            else
            {
                DeptTeam = SelectedTeamOption ?? string.Empty;
            }
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

        private void ExecuteClearForm()
        {
            DeptId = string.Empty;
            SupportKnown = false;
            SetTeamSelection(null);
            DeptNotes = string.Empty;
            _originalDept = null;
            _isNewEntry = false;
        }

        //Search handling
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

                    var deptContext = context with { Mode = nameof(AdminSection.Department) };
                    var result = await searchService.SearchAsync(deptContext, SearchTarget.Admin);

                    if (result is IDepartment dept)
                    {
                        var loaded = new Department
                        {
                            Number = dept.Number,
                            SupportKnown = dept.SupportKnown,
                            Team = dept.Team,
                            Notes = dept.Notes,
                            FileRepoPath = dept.FileRepoPath
                        };

                        _originalDept = new Department
                        {
                            Number = dept.Number,
                            SupportKnown = dept.SupportKnown,
                            Team = dept.Team,
                            Notes = dept.Notes,
                            FileRepoPath = dept.FileRepoPath
                        };

                        LoadDepartmentIntoForm(loaded, isNew: false);
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
                    UiNotify.Warn("Support Team search is not yet integrated.");
                    break;

                case AdminSection.Links:
                    UiNotify.Warn("Links search is not yet integrated.");
                    break;
            }
        }

        //Staing and queue
        private void ExecuteApply()
        {
            if (string.IsNullOrWhiteSpace(DeptId))
            {
                UiNotify.Warn("No entity loaded to apply changes to.");
                return;
            }

            switch (SelectedSection)
            {
                case AdminSection.Department:
                    StageDepartmentChange();
                    break;
                case AdminSection.SupportTeam:
                    UiNotify.Warn("Support Team staging is not yet integrated.");
                    break;
                case AdminSection.Links:
                    UiNotify.Warn("Links staging is not yet integrated.");
                    break;
            }
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

        //Save and export
        private async Task ExecuteSaveAsync()
        {
            if (!HasPendingChanges)
            {
                UiNotify.Warn("No staged changes to save.");
                return;
            }

            try
            {
                string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "departments.json");

                DepartmentListWrapper wrapper;
                if (File.Exists(targetPath))
                {
                    string existingJson = await File.ReadAllTextAsync(targetPath);
                    wrapper = JsonConvert.DeserializeObject<DepartmentListWrapper>(existingJson) ?? new();
                }
                else
                {
                    wrapper = new DepartmentListWrapper();
                }

                foreach (var change in StagedChanges.Where(c => c.Section == AdminSection.Department))
                {
                    if (change.StagedData is Department stagedDept)
                    {
                        var target = wrapper.DepartmentList.FirstOrDefault(d => d.Number == stagedDept.Number);
                        if (target != null)
                        {
                            target.SupportKnown = stagedDept.SupportKnown;
                            target.Team = stagedDept.Team;
                            target.Notes = stagedDept.Notes;
                        }
                        else
                        {
                            wrapper.DepartmentList.Add(stagedDept);
                        }
                    }
                }

                wrapper.Meta.LastUpdatedUtc = DateTime.UtcNow;

                string formattedJson = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
                await File.WriteAllTextAsync(targetPath, formattedJson);

                int savedCount = StagedChanges.Count;
                StagedChanges.Clear();
                NotifyStagingChanged();
                LoadAvailableTeams(); // Refresh dropdown

                Log.Info("Admin", $"Successfully saved {savedCount} staged change(s) to {targetPath}");
                UiNotify.Success($"Saved {savedCount} change(s) to departments.json!");
            }
            catch (Exception ex)
            {
                Log.Error("Admin", "Failed to write JSON file.", ex);
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