using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public class ComputerViewModel : ObeservableObject, ISearchableViewModel, IDisposable
    {
        // Services
        private readonly IADService _ad;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;

        private const string ViewKey = "ComputerView";

        // Errors / state
        private string? _error;
        public string? Error { get => _error; private set { _error = value; OnPropertyChanged(nameof(Error)); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); } }

        // FlowDoc text (bound to viewer)
        private string _searchLog = string.Empty;
        public string SearchLog { get => _searchLog; private set { _searchLog = value; OnPropertyChanged(nameof(SearchLog)); } }

        // Effective output font size for this view (the View listens and applies it to FlowDocument)
        private double _effectiveOutputFontSize;
        public double EffectiveOutputFontSize
        {
            get => _effectiveOutputFontSize;
            private set { _effectiveOutputFontSize = value; OnPropertyChanged(nameof(EffectiveOutputFontSize)); }
        }

        // Commands
        public ICommand ClearLogCommand { get; }
        public ICommand IncreaseFontCommand { get; }
        public ICommand DecreaseFontCommand { get; }
        public ICommand ResetFontCommand { get; }

        public ComputerViewModel(IADService adService, ISettingsService settingsSvc, IOutputTextSettingsProvider notifier)
        {
            _ad = adService ?? throw new ArgumentNullException(nameof(adService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));

            RefreshEffectiveFont();
            _notifier.Changed += OnOutputFontSettingsChanged;

            ClearLogCommand = new RelayCommand(_ => ClearLog());
            IncreaseFontCommand = new RelayCommand(_ => AdjustFont(+1));
            DecreaseFontCommand = new RelayCommand(_ => AdjustFont(-1));
            ResetFontCommand = new RelayCommand(_ => ResetFont());
        }

        private void OnOutputFontSettingsChanged(object? sender, EventArgs e) => RefreshEffectiveFont();

        private void RefreshEffectiveFont()
        {
            EffectiveOutputFontSize = _notifier.GetFontSize(ViewKey);
        }

        private void AdjustFont(int delta)
        {
            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _ = _settingsSvc.AdjustOutputFontSize(s, preferPerView ? ViewKey : null, delta, preferPerView);
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));

            _notifier.NotifyChanged();
        }

        private void ResetFont()
        {
            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _settingsSvc.ResetOutputFontSize(s, preferPerView ? ViewKey : null, preferPerView, defaultSize: 14);
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));

            _notifier.NotifyChanged();
        }

        // FlowDoc helpers
        private void AppendRaw(string message)
        {
            SearchLog += message + "\n";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void AppendTitle(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            AppendRaw($"[yellow]{text}[/yellow]");
        }

        private void AppendLabelValue(string label, string? value, bool treatEmptyAsNone = true)
        {
            var finalValue = value;
            if (string.IsNullOrWhiteSpace(finalValue) && treatEmptyAsNone) finalValue = "None";
            if (finalValue == null) return;
            AppendRaw($"[cyan]{label}[/cyan][red]{finalValue}[/red]");
        }

        // Search flow
        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;

            if (!string.IsNullOrEmpty(SearchLog))
            {
                AppendRaw("\n[cyan]────────── New Search ──────────[/cyan]\n");
                if (!string.IsNullOrWhiteSpace(context.Query))
                    AppendRaw($"[cyan]Query:[/cyan] [red]{context.Query}[/red]");
            }
            else if (!string.IsNullOrWhiteSpace(context.Query))
            {
                AppendRaw($"[cyan]Query:[/cyan] [red]{context.Query}[/red]");
            }

            Log.Info("ComputerView", $"Search started: target={target}, query='{context.Query}'");

            if (target != SearchTarget.Computer)
            {
                Error = "Invalid search target provided to ComputerViewModel.";
                AppendRaw("[red]Invalid search target for ComputerViewModel[/red]");
                Log.Warn("ComputerView", $"Invalid target: {target}");
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Query))
            {
                AppendRaw("[cyan]Query was null or whitespace.[/cyan]");
                Log.Info("ComputerView", "Aborted: empty query");
                return;
            }

            try
            {
                IsLoading = true;
                AppendRaw("[green]Starting computer search...[/green]");
                Log.Debug("ComputerView", "Dispatching directory search");

                var result = await searchService.SearchAsync(context, target);
                var comp = result as ADComputerInfo;

                if (comp is null || !comp.Exists)
                {
                    Error = comp?.ErrorMessage ?? "Computer not found.";
                    AppendRaw($"[red]Search complete. Computer not found. Error: {Error}[/red]");
                    Log.Info("ComputerView", $"Not found. Error='{Error}'");
                    return;
                }

                AppendRaw(string.Empty);
                AppendTitle(comp.Name);

                if (!string.IsNullOrWhiteSpace(comp.Description))
                    AppendLabelValue("Description: ", comp.Description);

                if (!string.IsNullOrWhiteSpace(comp.OperatingSystem))
                    AppendLabelValue("Operating System: ", comp.OperatingSystem);

                if (!string.IsNullOrWhiteSpace(comp.OUs))
                    AppendLabelValue("OUs: ", comp.OUs);

                if (!string.IsNullOrWhiteSpace(comp.LastLogonDate))
                    AppendLabelValue("Last Logon: ", comp.LastLogonDate);

                if (comp.Enabled == false)
                    AppendLabelValue("Enabled: ", "False", treatEmptyAsNone: false);

                AppendLabelValue("Hybrid Group Member: ", comp.IsHybridGroupMember ? "True" : "False", treatEmptyAsNone: false);

                Log.Info("ComputerView",
                    $"Computer found: Name='{comp.Name}', OS='{comp.OperatingSystem}', LastLogon='{comp.LastLogonDate}', Enabled={comp.Enabled}, Hybrid={comp.IsHybridGroupMember}, OUs='{comp.OUs}'");

                AppendRaw(string.Empty);
                Log.Info("ComputerView", "Search completed");
            }
            catch (Exception ex)
            {
                Error = $"Search failed: {ex.Message}";
                AppendRaw($"[red]Exception during computer search: {ex}[/red]");
                Log.Error("ComputerView", "Search failed", ex);
            }
            finally
            {
                IsLoading = false;
                AppendRaw("[green]Computer search process completed.[/green]");
            }
        }

        public void ClearLog() => SearchLog = string.Empty;

        // Dispose pattern for non-sealed type
        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _notifier.Changed -= OnOutputFontSettingsChanged;
            }
            _disposed = true;
        }
    }
}
