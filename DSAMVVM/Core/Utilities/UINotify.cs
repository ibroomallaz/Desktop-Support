using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Utilities
{
    // UI notification facade: modal alerts + status bar helpers.
    public static class UiNotify
    {
        // bus delegates (set at startup)
        private static Action<StatusItem>? _report;
        private static Action<string>? _removeKey;
        private static Action? _clear;

        // one-time init
        public static void Initialize(Action<StatusItem> report, Action<string>? removeKey = null, Action? clear = null)
        {
            _report = report;
            _removeKey = removeKey;
            _clear = clear;
        }

        // core forwarders
        public static void Push(StatusItem item) => _report?.Invoke(item);
        public static void RemoveKey(string key) => _removeKey?.Invoke(key);
        public static void ClearStatus() => _clear?.Invoke();

        // progress helpers (namespaced under a base "status thread" key)
        public static string ProgressOf(string baseKey) => baseKey + ".Progress";
        public static void Progress(string baseKey, string message, int priority = 0)
            => _report?.Invoke(new StatusItem(
                   Key: ProgressOf(baseKey),
                   Level: StatusLevel.Info,
                   Sticky: false,
                   Priority: priority,
                   Spans: new[] { StatusSpans.Text(message) }
               ));

        // Error: logs Error, optional status bar echo, then MessageBox
        public static string Error(string title, string message, Exception? ex = null, string? detailsPath = null, bool alsoStatusBar = true, string? key = null)
        {
            string code = NewRef();

            Log.Error("UiNotify", $"{title} [{code}]: {message}", ex);

            if (alsoStatusBar)
            {
                _report?.Invoke(new StatusItem(
                    Key: key ?? $"err:{code}",
                    Level: StatusLevel.Error,
                    Sticky: true,
                    Priority: 2,
                    Spans: new[] { StatusSpans.Bold($"{title}: {message}  (Ref: {code})") }
                ));
            }

            RunOnUi(() =>
            {
                var sb = new StringBuilder()
                    .AppendLine(message);
                if (detailsPath is not null)
                    sb.AppendLine().Append("Details: ").AppendLine(detailsPath);
                sb.AppendLine().Append("Reference: ").Append(code);

                MessageBox.Show(sb.ToString(), title, MessageBoxButton.OK, MessageBoxImage.Error);
            });

            return code;
        }

        // Warning: logs Warn and posts to status bar
        public static void Warn(string message, bool sticky = false, string? key = null)
        {
            Log.Warn("UiNotify", message);
            _report?.Invoke(new StatusItem(
                Key: key ?? NewRef("warn"),
                Level: StatusLevel.Warning,
                Sticky: sticky,
                Priority: 1,
                Spans: new[] { StatusSpans.Text(message) }
            ));
        }

        // Info: logs Info; optionally show on status bar (useful for quiet modes)
        public static void Info(string message, bool showStatusBar = false, string? key = null)
        {
            Log.Info("UiNotify", message);
            if (showStatusBar)
            {
                _report?.Invoke(new StatusItem(
                    Key: key ?? NewRef("info"),
                    Level: StatusLevel.Info,
                    Sticky: false,
                    Priority: 0,
                    Spans: new[] { StatusSpans.Text(message) }
                ));
            }
        }

        // Success is Info-level; by default also shows on the bar
        public static void Success(string message, bool showStatusBar = true, string? key = null)
        {
            Log.Success("UiNotify", message);
            if (showStatusBar)
            {
                _report?.Invoke(new StatusItem(
                    Key: key ?? NewRef("ok"),
                    Level: StatusLevel.Success,
                    Sticky: false,
                    Priority: 0,
                    Spans: new[] { StatusSpans.Text(message) }
                ));
            }
        }

        // Rich status with inline links/actions
        public abstract record StatusLink;
        public sealed record ExternalLink(string Text, Uri Uri, string? Tooltip = null) : StatusLink;
        public sealed record InternalAction(string Text, Action Action, string? Tooltip = null) : StatusLink;
        public sealed record InternalAsyncAction(string Text, Func<Task> Action, string? Tooltip = null) : StatusLink;

        public static class Link
        {
            public static StatusLink External(string text, Uri uri, string? tooltip = null) => new ExternalLink(text, uri, tooltip);

            public static StatusLink OpenLogs(string? customPath = null)
            {
                var path = customPath ?? (Log.LogsDirectoryPath ?? Environment.CurrentDirectory);
                if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
                var uri = new Uri(path);
                return new ExternalLink("Open logs", uri, "Open logs folder");
            }

            public static StatusLink Action(string text, Action onClick, string? tooltip = null) => new InternalAction(text, onClick, tooltip);
            public static StatusLink Action(string text, Func<Task> onClickAsync, string? tooltip = null) => new InternalAsyncAction(text, onClickAsync, tooltip);
        }

        public static void WarnWithLinks(string message, bool sticky = true, int priority = 1, string? key = null, params StatusLink[] links)
        {
            Log.Warn("UiNotify", message);

            var spans = new[] { StatusSpans.Text(message) }
                .Concat(links.Select(l => l switch
                {
                    ExternalLink ext => StatusSpans.Link(ext.Text, ext.Uri, ext.Tooltip),
                    InternalAction act => StatusSpans.Action(act.Text, RegisterCommand(() => { act.Action(); return Task.CompletedTask; }), tooltip: act.Tooltip),
                    InternalAsyncAction a => StatusSpans.Action(a.Text, RegisterCommand(a.Action), tooltip: a.Tooltip),
                    _ => StatusSpans.Text("") // safety
                }))
                .ToArray();

            _report?.Invoke(new StatusItem(
                Key: key ?? NewRef("warn"),
                Level: StatusLevel.Warning,
                Sticky: sticky,
                Priority: priority,
                Spans: spans
            ));
        }

        // UI helpers
        public static void RunOnUi(Action a)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp != null && !disp.CheckAccess()) disp.Invoke(a); else a();
        }

        public static void RunOnUiAsync(Action a, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp != null && !disp.CheckAccess()) disp.BeginInvoke(a, priority); else a();
        }

        // command registry (tokens used by renderer to execute internal actions)
        private static readonly ConcurrentDictionary<string, Func<Task>> _commands = new();

        public static string RegisterCommand(Func<Task> fn)
        {
            var key = "cmd:" + Guid.NewGuid().ToString("N")[..8];
            _commands[key] = fn;
            return key;
        }

        public static bool TryExecute(string commandToken)
        {
            if (_commands.TryGetValue(commandToken, out var fn))
            {
                FireAndLogAsync(fn);
                return true;
            }
            return false;
        }

        // internals
        private static void FireAndLogAsync(Func<Task> action) => _ = RunAsync(action);

        private static async Task RunAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { Error("Action failed", ex.Message, ex, alsoStatusBar: true); }
        }

        private static string NewRef(string prefix = "ref")
            => $"{prefix}:{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 26);
    }
}
