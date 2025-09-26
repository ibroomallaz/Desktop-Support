using System.Windows;
using System.Windows.Controls;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.ViewModel;

namespace DSAMVVM.MVVM.View
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext ??= new SettingsViewModel();
        }

        // --------- Footer buttons ---------

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && vm.ApplyCommand.CanExecute(null))
            {
                vm.ApplyCommand.Execute(null);
            }
        }

        private void BtnOpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && vm.OpenLogsCommand.CanExecute(null))
            {
                vm.OpenLogsCommand.Execute(null);
            }
        }

        // --------- Existing size handlers (safe placeholders) ---------

        private void InitialSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Keep if you have extra logic; otherwise, persistence flows through Apply
        }

        private void GeneralSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Keep if you have extra logic; otherwise, persistence flows through Apply
        }

        private void UsePerViewCheck_Changed(object sender, RoutedEventArgs e)
        {
            // Keep if you have extra logic; otherwise, persistence flows through Apply
        }

        private void PerViewSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Keep if you have extra logic; otherwise, persistence flows through Apply
        }

        // --------- Links mode radio group ---------

        private static void SaveLinksPrefs()
        {
            var svc = DSAMVVM.App.Services.GetService(typeof(ISettingsService)) as ISettingsService;
            svc?.RequestSave(DSAMVVM.App.Settings, Globals.g_SettingsPath); // debounced
        }

        private void LinksMode_LastViewed_Checked(object sender, RoutedEventArgs e)
        {
            var s = DSAMVVM.App.Settings?.Ui?.Links;
            if (s == null) return;

            // Enforce mutual exclusivity
            s.OpenLastViewedFirst = true;
            s.OverrideEnabled = false;

            SaveLinksPrefs();
        }

        private void LinksMode_Override_Checked(object sender, RoutedEventArgs e)
        {
            var s = DSAMVVM.App.Settings?.Ui?.Links;
            if (s == null) return;

            // Enforce mutual exclusivity
            s.OpenLastViewedFirst = false;
            s.OverrideEnabled = true;

            SaveLinksPrefs();
        }
    }
}
