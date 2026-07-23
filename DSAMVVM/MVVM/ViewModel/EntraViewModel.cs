using System.Windows.Input;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Services.Status;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Services.Graph;

namespace DSAMVVM.MVVM.ViewModel
{
    public class EntraViewModel : ObservableObject, ISearchableViewModel
    {
        private readonly IAuthenticationService _authService;
        private readonly TeamsRoutingService _routingService;
        private readonly StatusBus _statusBus;

        private string _searchQuery = string.Empty;
        private string _authButtonText = "Sign in";
        private bool _isSignedIn;

        public string SearchQuery
        {
            get => _searchQuery;
            set { _searchQuery = value; OnPropertyChanged(); }
        }

        public string AuthButtonText
        {
            get => _authButtonText;
            set { _authButtonText = value; OnPropertyChanged(); }
        }

        public ICommand SearchCommand { get; }
        public ICommand AuthToggleCommand { get; }

        // Injects dependencies for MSAL operations, GUID extraction, and global UI messaging
        public EntraViewModel(IAuthenticationService authService, TeamsRoutingService routingService, StatusBus statusBus)
        {
            _authService = authService;
            _routingService = routingService;
            _statusBus = statusBus;

            AuthToggleCommand = new RelayCommand(async _ => await ExecuteAuthToggleAsync());
            SearchCommand = AuthToggleCommand;
        }

        public Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target) => Task.CompletedTask;

        // Routes the command execution based on the tracked MSAL sign-in state
        private async Task ExecuteAuthToggleAsync()
        {
            if (_isSignedIn)
            {
                await ExecuteSignOutAsync();
            }
            else
            {
                await ExecuteSignInAsync();
            }
        }

        // Executes the MSAL token acquisition flow and publishes state changes via the builder pattern
        private async Task ExecuteSignInAsync()
        {
            Log.Info("EntraViewModel", "Initiating Entra ID token request.");

            _statusBus.Report(new StatusItemBuilder()
                .Key("EntraAuth")
                .Level(StatusLevel.Info)
                .Text("Initializing Entra ID token request...")
                .Build());

            try
            {
                string[] testScopes = ["User.Read"];
                string token = await _authService.GetGraphAccessTokenAsync(testScopes);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    Log.Success("EntraViewModel", "Entra ID sign-in completed successfully.");

                    _statusBus.Report(new StatusItemBuilder()
                        .Key("EntraAuth")
                        .Level(StatusLevel.Success)
                        .Text("Successfully signed in.")
                        .Build());

                    _isSignedIn = true;
                    AuthButtonText = "Sign out";
                }
            }
            catch (Exception ex)
            {
                Log.Error("EntraViewModel", "Entra ID sign-in exception encountered.", ex);

                _statusBus.Report(new StatusItemBuilder()
                    .Key("EntraAuth")
                    .Level(StatusLevel.Error)
                    .Sticky(true)
                    .Text($"AUTH FAILED: {ex.Message}")
                    .Build());
            }
        }

        // Clears the MSAL token cache for the active account and updates the UI status
        private async Task ExecuteSignOutAsync()
        {
            Log.Info("EntraViewModel", "Initiating Entra ID sign-out request.");

            try
            {
                await _authService.SignOutAsync();

                Log.Success("EntraViewModel", "Entra ID sign-out completed successfully.");

                _statusBus.Report(new StatusItemBuilder()
                    .Key("EntraAuth")
                    .Level(StatusLevel.Success)
                    .Text("Successfully signed out.")
                    .Build());

                _isSignedIn = false;
                AuthButtonText = "Sign in";
            }
            catch (Exception ex)
            {
                Log.Error("EntraViewModel", "Entra ID sign-out exception encountered.", ex);

                _statusBus.Report(new StatusItemBuilder()
                    .Key("EntraAuth")
                    .Level(StatusLevel.Error)
                    .Sticky(true)
                    .Text($"SIGN OUT FAILED: {ex.Message}")
                    .Build());
            }
        }
    }
}