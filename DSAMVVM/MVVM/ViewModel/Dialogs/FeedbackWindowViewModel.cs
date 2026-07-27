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
        private string _detailsText = string.Empty;
        private bool _isAuthenticated;

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

        public int SelectedFeedbackIndex
        {
            get => _selectedFeedbackIndex;
            set { _selectedFeedbackIndex = value; OnPropertyChanged(); }
        }

        public string DetailsText
        {
            get => _detailsText;
            set
            {
                _detailsText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSubmit));
            }
        }

        public bool CanSubmit => !IsAuthenticated || !string.IsNullOrWhiteSpace(DetailsText);

        public FeedbackWindowViewModel(
            IAuthenticationService authService,
            TeamsRoutingService routingService,
            IApplicationStateService appStateService)
        {
            _authService = authService;
            _routingService = routingService;
            _appStateService = appStateService;

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

            string feedbackType = SelectedFeedbackIndex == 0 ? "Bug" : "Request";
            string details = DetailsText.Trim();
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
                        _appStateService.RecentSupportTeam,
                        _appStateService.RecentQuery,
                        _appStateService.RecentDepartment
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