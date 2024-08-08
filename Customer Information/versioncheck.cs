using System;
using System.Windows.Forms;
public class Version
{
    static readonly HttpClient client = new HttpClient();
   static bool BetaCheck()
    {
        if (Program.version.ToLower().Contains("beta") || Program.version.ToLower().Contains("alpha"))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    public static async Task VersionCheck(string version)
    {

        try
        {
            using HttpResponseMessage response = await client.GetAsync("https://arizona.box.com/shared/static/5o9izr016qh0ywr8hsdk2f7vkijdl0xv.txt");
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            string checkedVersion = responseBody;
            if (checkedVersion != version & BetaCheck() == false)
            {
                    var result = MessageBox.Show($"Please update to Version {checkedVersion}. Current version: {version}.", "Desktop Support App: Alert", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
                    if (result == DialogResult.OK)
                    {
                        Program.OpenURL("https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx");
                    }
                
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
        }
    }
   
}
   