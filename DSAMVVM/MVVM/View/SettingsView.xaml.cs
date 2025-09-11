using System.Windows;
using System.Windows.Controls;
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


        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && vm.ApplyCommand.CanExecute(null))
                vm.ApplyCommand.Execute(null);
        }

        private void BtnOpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && vm.OpenLogsCommand.CanExecute(null))
                vm.OpenLogsCommand.Execute(null);
        }

        private void InitialSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void GeneralSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }
        private void UsePerViewCheck_Changed(object sender, RoutedEventArgs e) { }
        private void PerViewSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }
    }
}
