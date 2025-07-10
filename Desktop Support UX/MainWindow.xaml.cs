using Newtonsoft.Json;
using System.DirectoryServices;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using static QuickLinks;

namespace Desktop_Support_UX
{
    public partial class MainWindow : Window
    {

        private static Links? cachedLinks = null;
        private static readonly HttpClient client = new HttpClient();

        String outputText = "";
        public MainWindow()
        {
            _ = VersionChecker.VersionCheck();
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

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

            MessageBox.Show($"UITS Desktop Support App \n" + "Version 3.0.4\n" + "Developed by Isaac Broomall (ibroomall)\n"
                + "User Interface developed by JJ (jjvelasquez) :D", "About");
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {

            inputLabel.Content = "User Information: Enter NetID";
            textPlaceholder.Text = "User Info";

        }

        private void checkMIMButton_Checked(object sender, RoutedEventArgs e)
        {

            inputLabel.Content = "User Information: Enter Employee or StudentID";
            textPlaceholder.Text = "Employee/StudentID";

            txtInput.Clear();
        }

        private void checkMIMButton_Checked_1(object sender, RoutedEventArgs e)
        {

            inputLabel.Content = "Input Department Number MIM group you wish to check";
            textPlaceholder.Text = "Input Dept Number";

            txtInput.Clear();
        }

        private void computerInfoButton_Checked(object sender, RoutedEventArgs e)
        {

            inputLabel.Content = "Computer Information: Enter Hostname";
            textPlaceholder.Text = "Input Computer Info";

            txtInput.Clear();
        }

        private void reportMIMButton_Checked(object sender, RoutedEventArgs e)
        {

            inputLabel.Content = "Check current MIM groups";
            textPlaceholder.Text = "Enter NetID";

        }

        private void handleComputerInfo(String computerMenuText)
        {
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
            outputText += "-----------------------------------------------------------------------------\n";
            outputGrid.Text = outputText;
            txtInput.Clear();
        }

        public async Task handleUserInfo(String userMenuText)
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

                if (!string.IsNullOrEmpty(ADUser.DepartmentNumber))
                {
                    var department = await Globals.DepartmentService.GetDepartmentAsync(ADUser.DepartmentNumber);

                    if (department != null)
                    {
                        var serviceNowTeams = department.Teams?.Where(t => t.ServiceNow).ToList();
                        if (serviceNowTeams?.Any() == true)
                        {

                            outputText += "Service Now Team: " + string.Join(", ", serviceNowTeams.Select(t => t.Name)) + "\n";
                        }
                        else if (deptText.Teams?.Any() == true)
                        {
                            outputText += "Support Team: " + string.Join(", ", deptText.Teams.Select(t => t.Name)) + "\n";
                        }
                        else
                        {
                            outputText += "Teams: None\n";
                        }

                        if (await deptService.HasFileRepoAsync(ADUser.DepartmentNumber))
                        {
                            var fileRepo = await Globals.DepartmentService.GetFileRepoAsync(ADUser.DepartmentNumber);
                            if (fileRepo != null)
                            {
                                outputText += "File Repository: " + fileRepo.Location + "\n";
                            }
                            else
                            {
                                outputText += "File Repository: Details unavailable\n";
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(deptText.Notes))
                    {
                        outputText += "Notes: " + deptText.Notes + "\n";
                    }
                }
                else
                {
                    outputText += "Department information not found in cache.\n";
                }
            }
            else if (!string.IsNullOrWhiteSpace(ADUser.ErrorMessage))
            {
                outputText += "Error: " + ADUser.ErrorMessage + "\n";
            }
            else
            {
                outputText += userMenuText + " is not a Valid NetID\n";
            }

            outputText += "-----------------------------------------------------------------------------\n";
            outputGrid.Text = outputText;
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
                    outputText += ex.Message.ToString() + "\n-----------------------------------------------------------------------------\n";
                    outputGrid.Text = outputText;
                }
            }
            txtInput.Clear();
        }

        public void handleCheckMIMButton(String userMenuText)
        {
            string dept = "UA-MIM-0" + userMenuText;
            ADGroup group = new ADGroup(dept);
            if (!group.Exists)
            {
                outputText += "\nMIM Group " + dept + " does not exist.\n" + "-----------------------------------------------------------------------------\n";
                outputGrid.Text = outputText;
            }
            else
            {
                outputText += "\nTotal group members: " + group.MemberCount.ToString() + "\n";
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (var member in group.GroupMembers)
                {
                    outputText += member.ToString() + "\n";
                }
                outputText += "-----------------------------------------------------------------------------\n";
                outputGrid.Text = outputText;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }
            txtInput.Clear();
        }

        public void handleReportMIMButton(String userMenuText)
        {
            ADUserInfo user = new ADUserInfo(userMenuText);
            user.GetADMIMGroups(userMenuText);
            bool mimExists = user.MimGroupExists ?? false;
            Console.WriteLine();
            if (!user.Exists)
            {
                outputText += "\n" + userMenuText + " is not a Valid NetID" + "\n-----------------------------------------------------------------------------\n";
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
                outputText += "No valid MIM groups found for " + userMenuText + "\n-----------------------------------------------------------------------------\n";
                outputGrid.Text = outputText;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            outputText = "";
            outputGrid.Text = outputText;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(0);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(1);
        }
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(2);
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(3);
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(4);
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(5);
        }


        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(6);
        }

        private void Button_Click_10(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(7);
        }

        private void Button_Click_11(object sender, RoutedEventArgs e)
        {
            _ = handleLinkOpening(8);
        }

        public async Task handleLinkOpening(int LinkNumber)
        {
            if (cachedLinks == null)
            {
                try
                {
                    Console.WriteLine("Downloading Quicklinks data...");
                    string json = await client.GetStringAsync(Globals.g_QuickLinksURL);
                    cachedLinks = JsonConvert.DeserializeObject<Links>(json);
                    if (cachedLinks == null || cachedLinks.QL == null)
                    {
                        MessageBox.Show($"Deserialization resulted in null or invalid data.", "Error");
                        return;
                    }
                    String urlString = cachedLinks.QL[LinkNumber].URL;
                    HTTP.OpenURL(urlString);
                    cachedLinks = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error retrieving or deserializing JSON: " + ex, "Error");
                }
            }

        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            String userMenuText = txtInput.Text.Trim();
            Window window = new Window();
            window.Title = "Searching for " + userMenuText + " ...";
            window.Height = 25;
            window.Width = 400;
            window.Show();

            if (netIDButton.IsChecked == true)
            {
                _ = handleUserInfo(userMenuText);
            }
            else if (justIDButton.IsChecked == true)
            {
                handleJustIDButton(userMenuText);
            }
            else if (checkMIMButton.IsChecked == true)
            {
                handleCheckMIMButton(userMenuText);
            }
            else if (computerInfoButton.IsChecked == true)
            {
                handleComputerInfo(userMenuText);
            }
            else
            {
                handleReportMIMButton(userMenuText);
            }

            window.Close();
        }

    }
}