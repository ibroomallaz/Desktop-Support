using System;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
public class Version
{
   static bool BetaCheck()
    {
        if (Application.ProductVersion.ToLower().Contains("beta") || Application.ProductVersion.ToLower().Contains("alpha"))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    public static async Task VersionCheck()
    {
        try
        {
            HttpClient client = new HttpClient();   
            using HttpResponseMessage response = await client.GetAsync("https://arizona.box.com/shared/static/5o9izr016qh0ywr8hsdk2f7vkijdl0xv.txt");
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            string checkedVersion = responseBody;
            if (checkedVersion != Application.ProductVersion & BetaCheck() == false)
            {
                    var result = MessageBox.Show($"Please update to Version {checkedVersion}. Current version: {Application.ProductVersion}.", "Desktop Support App: Alert", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
                    if (result == DialogResult.OK)
                    {
                        HTTP.OpenURL("https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx");
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
   