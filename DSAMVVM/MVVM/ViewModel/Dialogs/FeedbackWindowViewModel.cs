using DSAMVVM.Core.Models;
using DSAMVVM.Core.Services.Graph;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
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
        private string _editableDepartment;
        private string _editableTeam;
        private string _noteText = string.Empty;

        // Backing fields for ServiceMeow nomination
        private string _petName = string.Empty;
        private string _petSpecies = "Cat";
        private string _petRole = "Chief Morale Officer";
        private string _petOwner = string.Empty;
        private string _petBlurb = string.Empty;

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
            init
            {
                _selectedFeedbackIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InputPromptText));
                OnPropertyChanged(nameof(IsBugOrRequestUI));
                OnPropertyChanged(nameof(IsUpdateUI));
                OnPropertyChanged(nameof(IsNoteUI));
                OnPropertyChanged(nameof(IsServiceMeowUI));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }

        // Visibility toggles for XAML elements
        public bool IsBugOrRequestUI => SelectedFeedbackIndex == 0 || SelectedFeedbackIndex == 1;
        public bool IsUpdateUI => SelectedFeedbackIndex == 2;
        public bool IsNoteUI => SelectedFeedbackIndex == 3;
        public bool IsServiceMeowUI => SelectedFeedbackIndex == 4;

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
            4 => "Nominate a team pet for ServiceMeow:",
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

        // ServiceMeow properties
        public string PetName
        {
            get => _petName;
            set { _petName = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSubmit)); }
        }

        public string PetSpecies
        {
            get => _petSpecies;
            set { _petSpecies = value; OnPropertyChanged(); }
        }

        public string PetRole
        {
            get => _petRole;
            set { _petRole = value; OnPropertyChanged(); }
        }

        public string PetOwner
        {
            get => _petOwner;
            set { _petOwner = value; OnPropertyChanged(); }
        }

        public string PetBlurb
        {
            get => _petBlurb;
            set { _petBlurb = value; OnPropertyChanged(); }
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
                    4 => !string.IsNullOrWhiteSpace(PetName),
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


        // Resolves the currently authenticated user's NetID
        private string GetPosterNetId()
        {
            string upn = _authService.CurrentAccountUpn ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(upn) && upn.Contains('@'))
            {
                return upn.Split('@')[0];
            }

            return Environment.UserName;
        }

        private static void OpenTeamsThreadDeepLink(string teamId, string channelId, string messageId)
        {
            try
            {
                // Format official Microsoft Teams message deep link
                string encodedChannel = WebUtility.UrlEncode(channelId);
                string deepLink = $"https://teams.microsoft.com/l/message/{encodedChannel}/{messageId}?groupId={teamId}&tenantId={Globals.EntraTenantId}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = deepLink,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FEEDBACK-DEBUG] Failed to launch Teams deep link: {ex.Message}");
            }
        }

        private async Task ExecuteSubmitAsync()
        {
            Debug.WriteLine("[FEEDBACK-DEBUG] ExecuteSubmitAsync command triggered.");

            if (!IsAuthenticated)
            {
                try
                {
                    Debug.WriteLine("[FEEDBACK-DEBUG] User is not authenticated. Triggering MSAL interactive prompt.");
                    string[] authScopes = ["User.Read", "ChannelMessage.Send"];
                    await _authService.AcquireTokenInteractiveAsync(authScopes);

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FEEDBACK-DEBUG] Auth Exception: {ex}");
                }
                return;
            }



            if (!CanSubmit)
            {
                Debug.WriteLine("[FEEDBACK-DEBUG] Execution aborted. CanSubmit returned false.");
                return;
            }

            string feedbackType = SelectedFeedbackIndex switch
            {
                0 => "Bug",
                1 => "Request",
                2 => "Update",
                3 => "Note",
                4 => "ServiceMeow",
                _ => "Bug"
            };

            // Intercepts Note to fetch Update channel ID, or ServiceMeow for pet nomination channel
            string routingKey = feedbackType switch
            {
                "Note" => "Update",
                "ServiceMeow" => "ServiceMeow",
                _ => feedbackType
            };

            // Compiles individual UI fields into a single details string for the payload
            string details = SelectedFeedbackIndex switch
            {
                0 or 1 => BugRequestText.Trim(),
                2 => ExpectedTeamText.Trim(),
                3 => NoteText.Trim(),
                4 => PetBlurb.Trim(),
                _ => string.Empty
            };

            Debug.WriteLine($"[FEEDBACK-DEBUG] Preparing to send: {feedbackType}. Details length: {details.Length}");

            try
            {
                string teamId = _routingService.TeamId;

                // Ensure we use the routingKey here instead of feedbackType
                string channelId = _routingService.GetChannelId(routingKey);

                Debug.WriteLine($"[FEEDBACK-DEBUG] Extracted Routing -> TeamID: {teamId} | ChannelID: {channelId}");

                if (!string.IsNullOrEmpty(teamId) && !string.IsNullOrEmpty(channelId))
                {
                    string[] scopes = ["ChannelMessage.Send"];

                    Debug.WriteLine("[FEEDBACK-DEBUG] Building Graph client and Token Provider...");
                    var tokenProvider = new InlineTokenProvider((AuthenticationService)_authService, scopes);
                    var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
                    var graphClient = new GraphServiceClient(authProvider);

                    Debug.WriteLine("[FEEDBACK-DEBUG] Awaiting GraphTeamsService.PostMessageAsync...");

                    string posterNetId = GetPosterNetId();

                    var payload = new FeedbackPayload(
                        TeamId: teamId,
                        ChannelId: channelId,
                        FeedbackType: feedbackType, // The payload retains the original type to format HTML properly
                        Details: details,
                        CurrentView: _appStateService.CurrentView,
                        RecentError: _appStateService.RecentError,
                        CurrentSupportTeam: EditableTeam.Trim(),
                        RecentQuery: _appStateService.RecentQuery,
                        RecentDepartment: EditableDepartment.Trim(),
                        PetName: PetName,
                        PetSpecies: PetSpecies,
                        PetRole: PetRole,
                        PetOwner: PetOwner,
                        PetBlurb: PetBlurb,
                        SubmitterNetId: posterNetId
                    );

                    var createdMessage = await GraphTeamsService.PostMessageAsync(graphClient, payload);

                    Debug.WriteLine($"[FEEDBACK-DEBUG] Graph post finished. CreatedMessage: {createdMessage?.Id}");

                    if (createdMessage != null)
                    {
                        // For ServiceMeow nominations, open Teams directly to the new message thread so user can reply with photo
                        if (feedbackType == "ServiceMeow" && !string.IsNullOrWhiteSpace(createdMessage.Id))
                        {
                            OpenTeamsThreadDeepLink(teamId, channelId, createdMessage.Id);
                        }

                        Debug.WriteLine("[FEEDBACK-DEBUG] Sending RequestClose event to window.");
                        RequestClose?.Invoke(this, true);
                    }
                }
                else
                {
                    Debug.WriteLine("[FEEDBACK-DEBUG] ABORT: Missing TeamID or ChannelID. Ensure routing claims exist in Entra token.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FEEDBACK-DEBUG] FATAL Submission Exception: {ex}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
