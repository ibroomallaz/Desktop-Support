using DSAMVVM.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DSAMVVM.MVVM.Behaviors
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

        private static readonly DependencyProperty SubscribedProviderProperty =
            DependencyProperty.RegisterAttached(
                "SubscribedProvider",
                typeof(IOutputTextSettingsProvider),
                typeof(FlowDocBinder),
                new PropertyMetadata(null));

        private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FlowDocumentScrollViewer viewer) return;

            // If DI isn't ready yet (XAML designer / early parse), delay until Loaded
            if (Application.Current is not App || App.Services == null)
            {
                viewer.Loaded -= DeferLoaded;
                viewer.Loaded += DeferLoaded;
                return;
            }

            EnsureSubscribedAndRender(viewer);
        }

        private static void DeferLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FlowDocumentScrollViewer viewer) return;

            // Try again; if DI still isn't ready, keep waiting
            if (Application.Current is App && App.Services != null)
            {
                viewer.Loaded -= DeferLoaded;
                EnsureSubscribedAndRender(viewer);
            }
        }

        private static void EnsureSubscribedAndRender(FlowDocumentScrollViewer viewer)
        {
            var sp = App.Services;
            var flowSvc = sp.GetRequiredService<IFlowDocService>();
            var textSettings = sp.GetRequiredService<IOutputTextSettingsProvider>();

            // Subscribe to settings changes once per viewer
            var current = (IOutputTextSettingsProvider?)viewer.GetValue(SubscribedProviderProperty);
            if (!ReferenceEquals(current, textSettings))
            {
                // Strong subscription + unhook on Unloaded keeps it simple and leak-free
                void Handler(object? _, EventArgs __)
                {
                    viewer.Dispatcher.Invoke(() => Rebuild(viewer, flowSvc));
                }

                // Remove previous (if any)
                if (current != null)
                    current.Changed -= Handler;

                textSettings.Changed += Handler;

                viewer.Unloaded += (_, __) =>
                {
                    // Unhook when viewer leaves the tree
                    textSettings.Changed -= Handler;
                    viewer.ClearValue(SubscribedProviderProperty);
                };

                viewer.SetValue(SubscribedProviderProperty, textSettings);
            }

            // Initial/updated render
            Rebuild(viewer, flowSvc);
        }

        private static void Rebuild(FlowDocumentScrollViewer viewer, IFlowDocService flowSvc)
        {
            var text = GetText(viewer) ?? string.Empty;
            var viewName = GetViewName(viewer); // null => default/global font size

            // Optionally preserve scroll position
            double? oldOffset = null;
            ScrollViewer? sv = null;
            if (GetPreserveScrollPosition(viewer))
            {
                sv = FindScrollViewer(viewer);
                if (sv != null) oldOffset = sv.VerticalOffset;
            }

            viewer.Document = flowSvc.BuildDocument(text, viewName: viewName);

            if (oldOffset is double o && sv != null)
                sv.ScrollToVerticalOffset(o);
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer s) return s;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var sv = FindScrollViewer(child);
                if (sv != null) return sv;
            }
            return null;
        }
    }
}
