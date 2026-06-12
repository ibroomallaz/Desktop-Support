using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public class EntraViewModel(IAuthenticationService authService) : ObservableObject, ISearchableViewModel
    {
        private string _searchQuery = string.Empty;
        private string _entraResults = "Ready to test authentication. Click Search to begin.";

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
            }
        }

        public string EntraResults
        {
            get => _entraResults;
            set
            {
                _entraResults = value;
                OnPropertyChanged();
            }
        }

        public ICommand SearchCommand => new RelayCommand(async _ => await ExecuteAuthTestAsync());

        public Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            // Intentionally left blank for the auth test phase
            return Task.CompletedTask;
        }

        private async Task ExecuteAuthTestAsync()
        {
            EntraResults = "Initializing Entra ID token request...";

            try
            {
                // Requesting a basic profile scope strictly to trigger the token generation loop
                string[] testScopes = ["User.Read"];
                string token = await authService.GetGraphAccessTokenAsync(testScopes);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    // Truncate the token string so we don't flood the UI layout with a massive JWT
                    string tokenSnippet = token.Length > 45 ? token.Substring(0, 45) + "..." : token;
                    EntraResults = $"SUCCESS!\n\nToken retrieved:\n{tokenSnippet}";
                }
            }
            catch (Exception ex)
            {
                EntraResults = $"AUTH FAILED:\n\n{ex.Message}";
            }
        }
    }
}