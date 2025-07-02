using Colors.Net;
using Colors.Net.StringColorExtensions;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.VisualBasic.Devices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.PropertyGridInternal;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Desktop_Support_UX
{
    //IDepartment teams = new IDepartment();
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void userInfo_Click(object sender, RoutedEventArgs e)
        {
            Window window = new Window();
            window.Title = "User Info";
            window.Height = 400;
            window.Width = 750;
            window.Show();


        }

        private void txtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInput.Text))
            {
                textPlaceholder.Visibility = Visibility.Visible;
            }
            else
            {
                textPlaceholder.Visibility = Visibility.Hidden;
            }
        }

        private void txtInput_TextChangedComputer(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtInputComputer.Text))
            {
                textPlaceholderComputer.Visibility = Visibility.Visible;
            }
            else
            {
                textPlaceholderComputer.Visibility = Visibility.Hidden;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            String userMenuText = txtInput.Text.Trim();
            Window window = new Window();
            window.Title = "Searching for " + userMenuText;
            window.Height = 25;
            window.Width = 400;
            window.Show();

            ADUserInfo ADUser = new ADUserInfo(userMenuText);
            String outputText = ADUser.DisplayName + "\n";
            if (ADUser.Exists)
            {
                if (!string.IsNullOrEmpty(ADUser.EduAffiliation))
                {
                    outputText += "Affiliation: " + ADUser.EduAffiliation + "\n";
                }
                if (!string.IsNullOrEmpty(ADUser.EduAffiliation))
                {
                    outputText += "Division: " + ADUser.Division + "\n";
                }
                if (!string.IsNullOrEmpty(ADUser.DepartmentName))
                {
                    outputText += "Department: " + ADUser.DepartmentName + "\n";
                }
                if (ADUser.Enabled == false)
                {
                    outputText += "Enabled: False" + "\n";
                }
                outputText += "O365 Licensing: " + ADUser.License + "\n";

                //if (!string.IsNullOrEmpty(ADUser.DepartmentNumber))
                //{
                //    var department = Globals.DepartmentService.GetDepartmentAsync(ADUser.DepartmentNumber);

                //    if (department != null)
                //    {
                //        var serviceNowTeams = department.Teams?.Where(t => t.ServiceNow).ToList();
                //        if (serviceNowTeams?.Any() == true)
                //        {
                //            ColoredConsole.Write($"{Cyan("Service Now Team: ")}");
                //            ColoredConsole.WriteLine(string.Join(", ", serviceNowTeams.Select(t => t.Name)).Red());
                //        }
                //        else if (department.Teams?.Any() == true)
                //        {
                //            ColoredConsole.Write($"{Cyan("Support Team: ")}");
                //            ColoredConsole.WriteLine(string.Join(", ", department.Teams.Select(t => t.Name)).Red());
                //        }
                //        else
                //        {
                //            ColoredConsole.WriteLine(Cyan("Teams: ") + "None".Red());
                //        }

                //        if (Globals.DepartmentService.HasFileRepoAsync(ADUser.DepartmentNumber))
                //        {
                //            var fileRepo = Globals.DepartmentService.GetFileRepoAsync(ADUser.DepartmentNumber);
                //            if (fileRepo != null)
                //            {
                //                ColoredConsole.Write($"{Cyan("File Repository: ")}");
                //                ColoredConsole.WriteLine(fileRepo.Location.Red());
                //            }
                //            else
                //            {
                //                ColoredConsole.WriteLine(Cyan("File Repository: ") + "Details unavailable".Red());
                //            }
                //        }

                //        if (!string.IsNullOrEmpty(department.Notes))
                //        {
                //            ColoredConsole.Write($"{Cyan("Notes: ")}");
                //            ColoredConsole.WriteLine(department.Notes.Red());
                //        }
                //    }
                //    else
                //    {
                //        ColoredConsole.WriteLine("Department information not found in cache.".Red());
                //    }
                //}
            }
            else if (!string.IsNullOrWhiteSpace(ADUser.ErrorMessage))
            {
                outputText += "Error: " + ADUser.ErrorMessage + "\n";
            }
            else
            {
                outputText += userMenuText + " is not a Valid NetID\n";
            }
            window.Close();
            outputGrid.Text += outputText + "-----------------------------------------------------------------------------\n";
            txtInput.Clear();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"UITS Desktop Support App \n" + "Version [pending feature]\n" + "Developed by Isaac Broomall (ibroomall)", "About");
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            String computerMenuText = txtInputComputer.Text.Trim();
            Window window = new Window();
            window.Title = "Searching for " + computerMenuText;
            window.Height = 25;
            window.Width = 400;
            window.Show();
            ADComputer ADComputer = new ADComputer(computerMenuText);
            String outputText = "";
            if (ADComputer.Exists)
            {
                outputText += "Location: " + ADComputer.OUs + "\n";
                if (!string.IsNullOrEmpty(ADComputer.Description))
                {
                    outputText += "Description: " + ADComputer.Description + "\n";
                }
                if (!string.IsNullOrEmpty(ADComputer.OperatingSystem))
                {
                    outputText += "Operating System: " + ADComputer.OperatingSystem + "\n";
                }
                if (ADComputer.Enabled == false)
                {
                    outputText += "Enabled: False\n";
                }
                outputText += "Hybrid Join Group: ";
                if (ADComputer.IsHybridGroupMember)
                {
                    outputText += "Yes\n";
                }
                else
                {
                    outputText += "No\n";
                }
            }
            else
            {
                outputText += computerMenuText + " is not in BlueCat.\n";
            }

            window.Close();
            outputGrid.Text += outputText + "-----------------------------------------------------------------------------\n";
            txtInputComputer.Clear();
        }
    }
}