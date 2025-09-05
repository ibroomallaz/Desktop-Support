using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace DSAMVVM.MVVM.View
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Ensure dropdown has a selection (prevents NRE)
            if (InitialSizeCombo != null && InitialSizeCombo.SelectedIndex < 0)
                InitialSizeCombo.SelectedIndex = 1; // Normal (14pt)

            ApplyInitialSizeFromCombo();

            // If per-tab is OFF, mirror per-tab to default
            if (UsePerViewCheck?.IsChecked != true)
                SyncPerViewToGeneral();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e) { }

        private void InitialSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyInitialSizeFromCombo();
            if (UsePerViewCheck?.IsChecked != true)
                SyncPerViewToGeneral();
        }

        private void GeneralSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (UsePerViewCheck?.IsChecked != true)
                SyncPerViewToGeneral();
        }

        private void UsePerViewCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (UsePerViewCheck?.IsChecked != true)
                SyncPerViewToGeneral();
        }

        private void PerViewSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // No-op
        }

        private void ApplyInitialSizeFromCombo()
        {
            double sizePt = 14;
            if (InitialSizeCombo?.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    sizePt = v;
                else if (item.Tag is double d)
                    sizePt = d;
            }

            sizePt = Clamp(sizePt, 8, 24);
            if (GeneralSizeSlider != null) GeneralSizeSlider.Value = sizePt;
        }

        private void SyncPerViewToGeneral()
        {
            var v = GeneralSizeSlider != null ? GeneralSizeSlider.Value : 14;
            v = Clamp(v, 8, 24);
            if (UserSize != null) UserSize.Value = v;
            if (ComputerSize != null) ComputerSize.Value = v;
            if (GroupSize != null) GroupSize.Value = v;
            if (EntraSize != null) EntraSize.Value = v;
        }

        private static double Clamp(double value, double min, double max)
            => Math.Max(min, Math.Min(max, value));

        private void BtnOpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            // TODO: wire up when services/paths are available
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // TODO: collect current UI values and push to settings service when wired:
            // - InitialSizeCombo (Tag)
            // - GeneralSizeSlider
            // - UsePerViewCheck
            // - UserSize, ComputerSize, GroupSize, EntraSize
            // - LogLevelCombo.SelectedIndex
            // - RetentionCombo (Tag days: -1 = indefinite)
            // - HistorySizeCombo (Tag items: 0 = Never)
        }
    }
}
