using Colors.Net;
using Colors.Net.StringColorExtensions;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.VisualBasic.Devices;
using System.DirectoryServices;
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

        String outputText = "";
        public MainWindow()
        {
            InitializeComponent();
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

            if (netIDButton.IsChecked == true)
            {
                handleUserInfo(userMenuText);
            }
            else if (justIDButton.IsChecked == true)
            {
                handleJustIDButton(userMenuText);
            }
            else if (checkMIMButton.IsChecked == true)
            {
                handleCheckMIMButton(userMenuText);
            }
            else
            {
                handleReportMIMButton(userMenuText);
            }
            window.Close();

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"UITS Desktop Support App \n" + "Version 3.0.4\n" + "Developed by Isaac Broomall (ibroomall)", "About");
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
            outputText += "-----------------------------------------------------------------------------\n";
            outputGrid.Text = outputText;
            txtInputComputer.Clear();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (netIDButton.IsChecked == true)
            {
                textPlaceholder.Text = "User Info";
            }
        }

        private void checkMIMButton_Checked(object sender, RoutedEventArgs e)
        {
            if (justIDButton.IsChecked == true)
            {
                textPlaceholder.Text = "Employee/StudentID";
            }
        }

        private void checkMIMButton_Checked_1(object sender, RoutedEventArgs e)
        {
            if (checkMIMButton.IsChecked == true)
            {
                textPlaceholder.Text = "Input Dept Number";
            }
        }

        private void reportMIMButton_Checked(object sender, RoutedEventArgs e)
        {
            if (reportMIMButton.IsChecked == true)
            {
                textPlaceholder.Text = "Enter NetID";
            }
        }

        public void handleUserInfo(String userMenuText)
        {
            ADUserInfo ADUser = new ADUserInfo(userMenuText);
            FileRepo repoText = new FileRepo();
            Team teamText = new Team();
            Department deptText = new Department();
            DepartmentService deptService = new DepartmentService();


            outputText += "\n" + ADUser.DisplayName + "\n";
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
                //    outputText += "MADE IT\n";
                //    var department = deptService.GetDepartmentAsync(ADUser.DepartmentNumber);

                //    if (department != null)
                //    {
                //        var serviceNowTeams = department.Teams?.Where(t => t.ServiceNow).ToList();
                //        if (serviceNowTeams?.Any() == true)
                //        {

                //            outputText += "Service Now Team: " + string.Join(", ", serviceNowTeams.Select(t => t.Name)) + "\n";
                //        }
                //        else if (deptText.Teams?.Any() == true)
                //        {
                //            outputText += "Support Team: " + string.Join(", ", deptText.Teams.Select(t => t.Name)) + "\n";
                //        }
                //        else
                //        {
                //            outputText += "Teams: None\n";
                //        }

                //        if (deptService.HasFileRepoAsync(ADUser.DepartmentNumber))
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


                //    }
                //    if (!string.IsNullOrEmpty(deptText.Notes))
                //    {
                //        outputText += "Notes: " + deptText.Notes + "\n";
                //    }
                //}
                //else
                //{
                //    outputText += "Department information not found in cache.\n";
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
            //window.Close();
            outputText += "-----------------------------------------------------------------------------\n";
            outputGrid.Text = outputText;
            txtInput.Clear();
        }

        public void handleJustIDButton(String userMenuText)
        {

            using (DirectoryEntry entry = new DirectoryEntry(Globals.g_domainPathLDAP))
            using (DirectorySearcher searcher = new DirectorySearcher(entry))
            {
                searcher.Filter = $"(&(objectClass=user)(employeeID={userMenuText}))";
                searcher.PropertiesToLoad.Add("displayName");
                searcher.PropertiesToLoad.Add("employeeID");

                try
                {
                    SearchResult? result = searcher.FindOne();
                    outputText += result != null ? result.Properties["displayName"][0].ToString() + 
                        "\n-----------------------------------------------------------------------------\n" ?? "Unknown" : "Employee/StudentID not found." +
                        "\n-----------------------------------------------------------------------------\n";
                    outputGrid.Text = outputText;
                }
                catch (Exception ex)
                {
                    outputText += ex.Message.ToString();
                    outputGrid.Text = outputText + "\n-----------------------------------------------------------------------------\n";
                }
            }
        }

        public void handleCheckMIMButton(String userMenuText)
        {
            string dept = "UA-MIM-0" + userMenuText;
            ADGroup group = new ADGroup(dept);
            if (!group.Exists)
            {
                outputText+="MIM Group " + dept + " does not exist.\n" + "-----------------------------------------------------------------------------\n";
                outputGrid.Text = outputText;
                //ColoredConsole.WriteLine($"MIM Group {DarkYellow(dept)} does {DarkRed("not")} exist.");
            }
            else
            {
                outputText += "\nTotal group members: " + group.MemberCount.ToString() + "\n";
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (var member in group.GroupMembers)
                {
                    outputText += member.ToString()+"\n";
                }
                outputText += "-----------------------------------------------------------------------------\n";
                outputGrid.Text = outputText;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }
        }

        public void handleReportMIMButton(String userMenuText)
        {
            ADUserInfo user = new ADUserInfo(userMenuText);
            user.GetADMIMGroups(userMenuText);
            bool mimExists = user.MimGroupExists ?? false;
            Console.WriteLine();
            if (!user.Exists)
            {
                outputText+=userMenuText + " is not a Valid NetID\n";
                outputGrid.Text = outputText;
                return;
            }
            if (user.Exists && mimExists)
            {
                outputText += "\n";
#pragma warning disable CS8602 // Dereference of a possibly null reference. Will not be null here
                foreach (var group in user.MimGroupsList)
                {
                    outputText += group.ToString() + "\n";
                }
                outputText += "-----------------------------------------------------------------------------\n";
                outputGrid.Text = outputText;
#pragma warning restore CS8602

            }
            else
            {
                outputText+="No valid MIM groups found for " + userMenuText + "\n-----------------------------------------------------------------------------\n";
                outputGrid.Text = outputText;
            }

        }
    }
}