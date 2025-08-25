using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using System;
using System.Text;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public class GroupViewModel(IADService adService) : ObeservableObject, ISearchableViewModel
    {
        private readonly IADService _ad = adService;

        private bool _isUserMim = true;
        public bool IsUserMim
        {
            get => _isUserMim;
            set { if (_isUserMim == value) return; _isUserMim = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsGroupMembers)); }
        }

        public bool IsGroupMembers
        {
            get => !_isUserMim;
            set { if (value == IsGroupMembers) return; _isUserMim = !value; OnPropertyChanged(nameof(IsUserMim)); OnPropertyChanged(); }
        }

        private string _query = string.Empty;
        public string Query { get => _query; set { _query = value?.Trim() ?? string.Empty; OnPropertyChanged(); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(); } }

        private string? _error;
        public string? Error { get => _error; private set { _error = value; OnPropertyChanged(); } }

        private readonly StringBuilder _log = new();
        public string SearchLog => _log.ToString();
        public void ClearLog() { _log.Clear(); OnPropertyChanged(nameof(SearchLog)); }

        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService _search, SearchTarget target)
        {
            if (!string.IsNullOrWhiteSpace(context?.Query)) Query = context.Query!.Trim();
            await ExecuteAsync();
        }

        public async Task ExecuteAsync()
        {
            Error = null;
            if (string.IsNullOrWhiteSpace(Query))
            {
                AppendLine($"Enter {(IsUserMim ? "a NetID" : "a group name or 4-digit dept#")} in the main search.");
                return;
            }

            IsLoading = true;
            try
            {
                if (IsUserMim)
                {
                    AppendHeader($"MIM groups for user '{Query}'");

                    var r = await _ad.GetUserMimGroupsAsync(Query);
                    if (!r.Exists)
                    {
                        AppendLine($"'{Query}' is not a valid NetID.");
                        if (!string.IsNullOrWhiteSpace(r.Error)) AppendLine(r.Error);
                    }
                    else
                    {
                        if (r.Enabled == false) AppendLine("Account is disabled.");
                        if (r.Groups.Count > 0)
                        {
                            AppendLine($"Total MIM groups: {r.Groups.Count}");
                            foreach (var g in r.Groups) AppendLine($"• {g}");
                        }
                        else AppendLine("No valid MIM groups found.");
                    }
                }
                else
                {
                    var groupName = NormalizeGroupName(Query);
                    AppendHeader($"Members of group '{groupName}'");

                    var info = await _ad.GetGroupAsync(groupName);
                    if (info.Exists && info.MemberCount is int c)
                    {
                        AppendLine($"Total members: {c}");
                        if (c == 0) AppendLine("No group members exist.");
                        else foreach (var m in info.GroupMembers ?? []) AppendLine($"• {m}");
                    }
                    else AppendLine(info.ErrorMessage ?? "Group not found or lookup failed.");
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                UiNotify.Error("Group query failed", ex.Message, ex, alsoStatusBar: true);
                AppendLine($"Error: {ex.Message}");
            }
            finally { IsLoading = false; }
        }

        private static string NormalizeGroupName(string input)
        {
            var s = input.Trim();
            if (s.Length == 4 && int.TryParse(s, out _)) return $"UA-MIM-0{s}";
            return s;
        }

        private void AppendLine(string text) { _log.AppendLine(text); OnPropertyChanged(nameof(SearchLog)); }
        private void AppendHeader(string title) { _log.AppendLine(); _log.AppendLine(title); _log.AppendLine(new string('─', Math.Clamp(title.Length, 8, 80))); OnPropertyChanged(nameof(SearchLog)); }
    }
}
