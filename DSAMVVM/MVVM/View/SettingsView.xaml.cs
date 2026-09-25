using DSAMVVM.MVVM.ViewModel;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DSAMVVM.MVVM.View
{
    public partial class SettingsView : UserControl
    {
        private SettingsViewModel? _subscribedVm;

        public SettingsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnsubscribeVm();
            if (e.NewValue is SettingsViewModel vm)
            {
                SubscribeVm(vm);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                if (_subscribedVm != vm)
                {
                    UnsubscribeVm();
                    SubscribeVm(vm);
                }

                if (!string.IsNullOrWhiteSpace(vm.PendingAnchor))
                {
                    var anchor = vm.PendingAnchor;
                    vm.PendingAnchor = null;
                    ScrollToAnchor(anchor);
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeVm();
        }

        private void SubscribeVm(SettingsViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.ScrollToAnchorRequested += OnScrollToAnchorRequested;
        }

        private void UnsubscribeVm()
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.ScrollToAnchorRequested -= OnScrollToAnchorRequested;
                _subscribedVm = null;
            }
        }

        private void OnScrollToAnchorRequested(string anchor)
        {
            ScrollToAnchor(anchor);
        }

        private void ScrollToAnchor(string anchor)
        {
            if (string.IsNullOrWhiteSpace(anchor)) return;

            Dispatcher.InvokeAsync(() =>
            {
                if (FindName(anchor) is FrameworkElement element)
                {
                    element.BringIntoView();
                    FlashElement(element);
                }
            }, DispatcherPriority.Loaded);
        }

        private static void FlashElement(FrameworkElement element)
        {
            var anim = new DoubleAnimation
            {
                From = 0.35,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(700),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(OpacityProperty, anim);
        }
    }
}
