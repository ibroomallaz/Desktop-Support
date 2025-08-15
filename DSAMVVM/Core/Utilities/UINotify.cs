using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model;

namespace DSAMVVM.Core.Utilities
{
    // UI notification facade: modal alerts + status bar helpers.
    public static class UiNotify
    {
        private static StatusBus? _bus;

        // Initialize once at startup with the shared StatusBus instance
        public static void Initialize(StatusBus bus) => _bus = bus;

        public static void Push(StatusMessage message) => _bus?.Report(message);

        // Error: logs Error, optional status bar echo, then MessageBox
        public static string Error(string title, string message, Exception? ex = null, string? detailsPath = null, bool alsoStatusBar = true, string? key = null)
        {
            string code = NewRef();

            Log.Error("UiNotify", $"{title} [{code}]: {message}", ex);

            if (alsoStatusBar && _bus != null)
            {
                var bar = StatusMessageFactory.Error($"{title}: {message}  (Ref: {code})",
                                                     priority: 2, sticky: true, key: key ?? $"err:{code}");
                _bus.Report(bar);
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
            _bus?.Report(StatusMessageFactory.Warning(message, priority: 1, sticky: sticky, key: key));
        }
        // Info: logs Info; optionally show on status bar (useful for quiet modes)
        public static void Info(string message, bool showStatusBar = false, string? key = null)
        {
            Log.Info("UiNotify", message);
            if (showStatusBar)
                _bus?.Report(StatusMessageFactory.Info(message, priority: 0, sticky: false, key: key));
        }
        // Success is Info-level; by default also shows on the bar
        public static void Success(string message, bool showStatusBar = true, string? key = null)
        {
            Log.Success("UiNotify", message);
            if (showStatusBar)
                _bus?.Report(StatusMessageFactory.Success(message, priority: 0, sticky: false, key: key));
        }

        // Domain-specific language for rich status messages
        public abstract record StatusLink;
        public sealed record ExternalLink(string Text, Uri Uri) : StatusLink;
        public sealed record InternalAction(string Text, Action Action) : StatusLink;
        public sealed record InternalAsyncAction(string Text, Func<Task> ActionAsync) : StatusLink;

        public static class Link
        {
            public static StatusLink External(string text, Uri uri) => new ExternalLink(text, uri);

            public static StatusLink OpenLogs(string? customPath = null)
            {
                var path = customPath ?? (Log.LogsDirectoryPath ?? Environment.CurrentDirectory);
                if (!Path.IsPathRooted(path)) path = Path.GetFullPath(path);
                var uri = new Uri(path);
                return new ExternalLink("Open logs", uri);
            }

            public static StatusLink Action(string text, Action onClick) => new InternalAction(text, onClick);
            public static StatusLink Action(string text, Func<Task> onClickAsync) => new InternalAsyncAction(text, onClickAsync);
        }

        public static void WarnWithLinks(string message, bool sticky = true, int priority = 1, string? key = null, params StatusLink[] links)
        {
            Log.Warn("UiNotify", message);
            if (_bus is null) return;

            Inline[] inlines = links.Select(l => l switch
            {
                ExternalLink ext => StatusMessageFactory.Link(ext.Text, ext.Uri),
                InternalAction act => StatusMessageFactory.ActionLink(act.Text, act.Action),
                InternalAsyncAction aact => StatusMessageFactory.ActionLink(aact.Text, () => FireAndLogAsync(aact.ActionAsync)),
                _ => new Run("")
            }).ToArray();

            string format = inlines.Length > 0
                ? message + " " + string.Join(" ", Enumerable.Range(0, inlines.Length).Select(i => $"{{{i}}}"))
                : message;

            var rich = StatusMessageFactory.CreateRichInternalMessage(format, inlines, priority, sticky, key);
            _bus.Report(rich);
        }

        // - UI Helpers

        // Blocking invoke (good for messagebox dialogs)
        public static void RunOnUi(Action a)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp != null && !disp.CheckAccess()) disp.Invoke(a); else a();
        }

        // Non-blocking begininvoke (Status bar updates, animations, background UI changes)
        public static void RunOnUiAsync(Action a, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp != null && !disp.CheckAccess()) disp.BeginInvoke(a, priority); else a();
        }

        // internals
        private static void FireAndLogAsync(Func<Task> action) => _ = RunAsync(action);

        private static async Task RunAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { Error("Action failed", ex.Message, ex, alsoStatusBar: true); }
        }

        private static string NewRef()
            => $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 22);
    }
}
