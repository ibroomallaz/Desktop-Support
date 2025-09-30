using DSAMVVM.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DSAMVVM.MVVM.Model.Behaviors
{
    public static class FlowDocBinder
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached(
                "Text",
                typeof(string),
                typeof(FlowDocBinder),
                new PropertyMetadata(string.Empty, OnAnyPropertyChanged));

        public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);
        public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);

        public static readonly DependencyProperty ViewNameProperty =
            DependencyProperty.RegisterAttached(
                "ViewName",
                typeof(string),
                typeof(FlowDocBinder),
                new PropertyMetadata(null, OnAnyPropertyChanged));

        public static void SetViewName(DependencyObject obj, string? value) => obj.SetValue(ViewNameProperty, value);
        public static string? GetViewName(DependencyObject obj) => (string?)obj.GetValue(ViewNameProperty);

        public static readonly DependencyProperty PreserveScrollPositionProperty =
            DependencyProperty.RegisterAttached(
                "PreserveScrollPosition",
                typeof(bool),
                typeof(FlowDocBinder),
                new PropertyMetadata(true));

        public static void SetPreserveScrollPosition(DependencyObject obj, bool value) =>
            obj.SetValue(PreserveScrollPositionProperty, value);
        public static bool GetPreserveScrollPosition(DependencyObject obj) =>
            (bool)obj.GetValue(PreserveScrollPositionProperty);

        // Scroll hard to bottom when Text changes (search) -> default on
        public static readonly DependencyProperty ScrollToBottomOnTextChangeProperty =
            DependencyProperty.RegisterAttached(
                "ScrollToBottomOnTextChange",
                typeof(bool),
                typeof(FlowDocBinder),
                new PropertyMetadata(true));

        public static void SetScrollToBottomOnTextChange(DependencyObject obj, bool value) =>
            obj.SetValue(ScrollToBottomOnTextChangeProperty, value);
        public static bool GetScrollToBottomOnTextChange(DependencyObject obj) =>
            (bool)obj.GetValue(ScrollToBottomOnTextChangeProperty);

        private static readonly DependencyProperty SubscribedProviderProperty =
            DependencyProperty.RegisterAttached(
                "SubscribedProvider",
                typeof(IOutputTextSettingsProvider),
                typeof(FlowDocBinder),
                new PropertyMetadata(null));

        private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FlowDocumentScrollViewer viewer) return;

            // If DI isn't ready yet (designer / early parse), delay until Loaded
            if (Application.Current is not App || App.Services == null)
            {
                viewer.Loaded -= DeferLoaded;
                viewer.Loaded += DeferLoaded;
                return;
            }

            // -> yank only when Text changed (new search)
            bool forceBottom = e.Property == TextProperty && GetScrollToBottomOnTextChange(viewer);
            EnsureSubscribedAndRender(viewer, forceBottom);
        }

        private static void DeferLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FlowDocumentScrollViewer viewer) return;

            if (Application.Current is App && App.Services != null)
            {
                viewer.Loaded -= DeferLoaded;
                EnsureSubscribedAndRender(viewer, forceBottom: false);
            }
        }

        private static void EnsureSubscribedAndRender(FlowDocumentScrollViewer viewer, bool forceBottom)
        {
            var sp = App.Services;
            var flowSvc = sp.GetRequiredService<IFlowDocService>();
            var textSettings = sp.GetRequiredService<IOutputTextSettingsProvider>();

            // Subscribe to settings changes once per viewer
            var current = (IOutputTextSettingsProvider?)viewer.GetValue(SubscribedProviderProperty);
            if (!ReferenceEquals(current, textSettings))
            {
                void Handler(object? _, EventArgs __)
                {
                    viewer.Dispatcher.Invoke(() => Rebuild(viewer, flowSvc, forceBottom: false)); // settings change -> preserve pos
                }

                if (current != null)
                    current.Changed -= Handler; // safe no-op if not the same delegate instance

                textSettings.Changed += Handler;

                viewer.Unloaded += (_, __) =>
                {
                    textSettings.Changed -= Handler;
                    viewer.ClearValue(SubscribedProviderProperty);
                };

                viewer.SetValue(SubscribedProviderProperty, textSettings);
            }

            // Initial/updated render
            Rebuild(viewer, flowSvc, forceBottom);
        }

        private static void Rebuild(FlowDocumentScrollViewer viewer, IFlowDocService flowSvc, bool forceBottom)
        {
            var text = GetText(viewer) ?? string.Empty;
            var viewName = GetViewName(viewer); // null => default/global font size

            // Preserve scroll only if not forcing bottom
            double? oldOffset = null;
            ScrollViewer? sv = null;
            if (!forceBottom && GetPreserveScrollPosition(viewer))
            {
                sv = FindScrollViewer(viewer);
                if (sv != null) oldOffset = sv.VerticalOffset;
            }

            viewer.Document = flowSvc.BuildDocument(text, viewName: viewName);

            if (forceBottom)
            {
                // -> wait for layout, then snap to bottom
                viewer.Dispatcher.InvokeAsync(() =>
                {
                    var s = FindScrollViewer(viewer);
                    s?.ScrollToBottom();
                }, DispatcherPriority.Background);
            }
            else if (oldOffset is double o && sv != null)
            {
                sv.ScrollToVerticalOffset(o);
            }
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer s) return s;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var found = FindScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
