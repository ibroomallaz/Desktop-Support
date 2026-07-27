using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Services.Graph;
using DSAMVVM.Core.Utilities;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel.Dialogs
{
    public sealed class FeedbackWindowViewModel : INotifyPropertyChanged
    {
        private readonly IAuthenticationService _authService;
        private readonly TeamsRoutingService _routingService;
        private readonly IApplicationStateService _appStateService;

        private int _selectedFeedbackIndex;
        private bool _isAuthenticated;

        // Backing fields for dynamic UI input binding
        private string _bugRequestText = string.Empty;
        private string _expectedTeamText = string.Empty;
        private string _editableDepartment = string.Empty;
        private string _editableTeam = string.Empty;
        private string _noteText = string.Empty;

        public event EventHandler<bool>? RequestClose;
        public ICommand SubmitCommand { get; }

        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set
            {
                if (_isAuthenticated != value)
                {
                    _isAuthenticated = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SubmitButtonText));
                    OnPropertyChanged(nameof(CanSubmit));
                }
            }
        }

        public string SubmitButtonText => IsAuthenticated ? "Submit" : "Sign-In";

        // Controls active UI state and triggers visibility re-evaluation upon change
        public int SelectedFeedbackIndex
        {
            get => _selectedFeedbackIndex;
            set
            {
                _selectedFeedbackIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InputPromptText));
                OnPropertyChanged(nameof(IsBugOrRequestUI));
                OnPropertyChanged(nameof(IsUpdateUI));
                OnPropertyChanged(nameof(IsNoteUI));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }

        // Visibility toggles for XAML elements
        public bool IsBugOrRequestUI => SelectedFeedbackIndex == 0 || SelectedFeedbackIndex == 1;
        public bool IsUpdateUI => SelectedFeedbackIndex == 2;
        public bool IsNoteUI => SelectedFeedbackIndex == 3;

        // Editable context properties initialized from application state
        public string EditableDepartment
        {
            get => _editableDepartment;
            set { _editableDepartment = value; OnPropertyChanged(); }
        }

        public string EditableTeam
        {
            get => _editableTeam;
            set { _editableTeam = value; OnPropertyChanged(); }
        }

        // Routes the instructional header text above the input controls
        public string InputPromptText => SelectedFeedbackIndex switch
        {
            0 => "Provide bug details below:",
            1 => "Describe your feature request:",
            _ => "Provide details below:"
        };

        public string BugRequestText
        {
            get => _bugRequestText;
            set { _bugRequestText = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
        }

        public string ExpectedTeamText
        {
            get => _expectedTeamText;
            set { _expectedTeamText = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
        }

        public string NoteText
        {
            get => _noteText;
            set { _noteText = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
        }

        // Validates required fields based on the currently active UI index
        public bool CanSubmit
        {
            get
            {
                if (!IsAuthenticated) return true;

                return SelectedFeedbackIndex switch
                {
                    0 or 1 => !string.IsNullOrWhiteSpace(BugRequestText),
                    2 => !string.IsNullOrWhiteSpace(ExpectedTeamText) && !string.IsNullOrWhiteSpace(EditableDepartment),
                    3 => !string.IsNullOrWhiteSpace(NoteText) && !string.IsNullOrWhiteSpace(EditableDepartment),
                    _ => false
                };
            }
        }

        public FeedbackWindowViewModel(
            IAuthenticationService authService,
            TeamsRoutingService routingService,
            IApplicationStateService appStateService)
        {
            _authService = authService;
            _routingService = routingService;
            _appStateService = appStateService;

            // Initialize editable fields from application state defaults
            _editableDepartment = string.IsNullOrWhiteSpace(_appStateService.RecentDepartment) ? string.Empty : _appStateService.RecentDepartment;
            _editableTeam = string.IsNullOrWhiteSpace(_appStateService.RecentSupportTeam) ? string.Empty : _appStateService.RecentSupportTeam;

            _isAuthenticated = _authService.IsAuthenticated;
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;

            SubmitCommand = new RelayCommand(async _ => await ExecuteSubmitAsync());
        }

        private void OnAuthenticationStateChanged(bool isAuthenticated)
        {
            IsAuthenticated = isAuthenticated;
        }

        private async Task ExecuteSubmitAsync()
        {
            System.Diagnostics.Debug.WriteLine("[FEEDBACK-DEBUG] ExecuteSubmitAsync command triggered.");

            if (!IsAuthenticated)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[FEEDBACK-DEBUG] User is not authenticated. Triggering MSAL interactive prompt.");
                    string[] authScopes = ["User.Read"];
                    await _authService.AcquireTokenInteractiveAsync(authScopes);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FEEDBACK-DEBUG] Auth Exception: {ex}");
                }
                return;
            }

            if (!CanSubmit)
            {
                System.Diagnostics.Debug.WriteLine("[FEEDBACK-DEBUG] Execution aborted. CanSubmit returned false.");
                return;
            }

            string feedbackType = SelectedFeedbackIndex switch
            {
                0 => "Bug",
                1 => "Request",
                2 => "Update",
                3 => "Note",
                _ => "Bug"
            };

            // Compiles individual UI fields into a single details string for the payload
            string details = SelectedFeedbackIndex switch
            {
                0 or 1 => BugRequestText.Trim(),
                2 => ExpectedTeamText.Trim(),
                3 => NoteText.Trim(),
                _ => string.Empty
            };

            System.Diagnostics.Debug.WriteLine($"[FEEDBACK-DEBUG] Preparing to send: {feedbackType}. Details length: {details.Length}");

            try
            {
                string teamId = _routingService.TeamId;
                string channelId = _routingService.GetChannelId(feedbackType);

                System.Diagnostics.Debug.WriteLine($"[FEEDBACK-DEBUG] Extracted Routing -> TeamID: {teamId} | ChannelID: {channelId}");

                if (!string.IsNullOrEmpty(teamId) && !string.IsNullOrEmpty(channelId))
                {
                    string[] scopes = ["ChannelMessage.Send"];

                    System.Diagnostics.Debug.WriteLine("[FEEDBACK-DEBUG] Building Graph client and Token Provider...");
                    var tokenProvider = new InlineTokenProvider((AuthenticationService)_authService, scopes);
                    var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
                    var graphClient = new GraphServiceClient(authProvider);

                    System.Diagnostics.Debug.WriteLine("[FEEDBACK-DEBUG] Awaiting GraphTeamsService.PostMessageAsync...");

                    var payload = new FeedbackPayload(
                        teamId,
                        channelId,
                        feedbackType,
                        details,
                        _appStateService.CurrentView,
                        _appStateService.RecentError,
                        EditableTeam.Trim(),
                        _appStateService.RecentQuery,
                        EditableDepartment.Trim()
                    );

                    bool success = await GraphTeamsService.PostMessageAsync(graphClient, payload);

                    System.Diagnostics.Debug.WriteLine($"[FEEDBACK-DEBUG] Graph post finished. Success: {success}");

                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine("[FEEDBACK-DEBUG] Sending RequestClose event to window.");
                        RequestClose?.Invoke(this, true);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[FEEDBACK-DEBUG] ABORT: Missing TeamID or ChannelID. Ensure routing claims exist in Entra token.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FEEDBACK-DEBUG] FATAL Submission Exception: {ex}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}