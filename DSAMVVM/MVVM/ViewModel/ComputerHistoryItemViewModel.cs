using System.Windows;
using System.Windows.Input;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;

namespace DSAMVVM.MVVM.ViewModel
{
    public class ComputerHistoryItemViewModel : ObservableObject
    {
        private readonly IADService _adService;
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

        private ADComputerInfo? _computer;
        public ADComputerInfo? Computer
        {
            get => _computer;
            private set
            {
                _computer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFound));
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(ComputerName));
                OnPropertyChanged(nameof(OperatingSystem));
                OnPropertyChanged(nameof(IsWindows11));
                OnPropertyChanged(nameof(IsWindows10));
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(HasDescription));
                OnPropertyChanged(nameof(CleanOuPath));
                OnPropertyChanged(nameof(HasOUs));
                OnPropertyChanged(nameof(LastLogonDate));
                OnPropertyChanged(nameof(HasLastLogon));
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(IsDisabled));
                OnPropertyChanged(nameof(IsHybridGroupMember));
            }
        }

        public bool IsFound => Computer?.Exists == true;
        public string? ErrorMessage => Computer?.ErrorMessage;

        public string ComputerName => !string.IsNullOrWhiteSpace(Computer?.Name) ? Computer.Name : Query;
        public string OperatingSystem => !string.IsNullOrWhiteSpace(Computer?.OperatingSystem) ? Computer.OperatingSystem : "Unknown OS";
        public bool IsWindows11 => OperatingSystem.Contains("Windows 11", StringComparison.OrdinalIgnoreCase);
        public bool IsWindows10 => OperatingSystem.Contains("Windows 10", StringComparison.OrdinalIgnoreCase);

        public string? Description => Computer?.Description;
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        public string CleanOuPath => CleanOuString(Computer?.OUs);
        public bool HasOUs => !string.IsNullOrWhiteSpace(CleanOuPath);

        public string? LastLogonDate => Computer?.LastLogonDate;
        public bool HasLastLogon => !string.IsNullOrWhiteSpace(LastLogonDate) &&
                                    !LastLogonDate.Equals("Never", StringComparison.OrdinalIgnoreCase) &&
                                    !LastLogonDate.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

        public bool IsEnabled => Computer?.Exists == true && Computer.Enabled != false;
        public bool IsDisabled => Computer is { Exists: true, Enabled: false };
        public bool IsHybridGroupMember => Computer?.IsHybridGroupMember == true;

        // Copy State
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
        public ICommand CopyNameCommand { get; }
        public ICommand RefreshCommand { get; }

        public ComputerHistoryItemViewModel(
            string query,
            ADComputerInfo? computer,
            IADService adService,
            IDeepLinkRoutingService linkRouter)
        {
            Query = query;
            Timestamp = DateTime.Now;
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            Computer = computer;

            ToggleExpandCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
            CopyNameCommand = new RelayCommand(_ => CopyComputerName());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
        }

        private void CopyComputerName()
        {
            if (string.IsNullOrWhiteSpace(ComputerName)) return;
            try
            {
                Clipboard.SetDataObject(ComputerName);
                IsCopied = true;
                _ = Task.Delay(1500).ContinueWith(_ => UiNotify.RunOnUiAsync(() => IsCopied = false));
            }
            catch { /* Clipboard access can fail if occupied */ }
        }

        private async Task RefreshAsync()
        {
            if (IsRefreshing || string.IsNullOrWhiteSpace(ComputerName)) return;
            try
            {
                IsRefreshing = true;
                var updated = await _adService.GetComputerAsync(ComputerName);
                Computer = updated;
                Timestamp = DateTime.Now;
                OnPropertyChanged(nameof(TimeFormatted));
                OnPropertyChanged(nameof(RelativeAgeText));
                OnPropertyChanged(nameof(IsStale));
            }
            catch (Exception ex)
            {
                Log.Error("ComputerHistoryItem", $"Failed to refresh computer '{ComputerName}'", ex);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private static string CleanOuString(string? rawOus)
        {
            if (string.IsNullOrWhiteSpace(rawOus)) return string.Empty;

            var parts = rawOus.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .Where(p => p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
                              .Select(p => p[3..].Trim())
                              .Reverse()
                              .ToList();

            return parts.Count > 0 ? string.Join(" \u203a ", parts) : rawOus;
        }
    }
}
