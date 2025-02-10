public class VersionOld
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
            using HttpResponseMessage response = await client.GetAsync(Globals.g_versionTxt);
            response.EnsureSuccessStatusCode();
            string checkedVersion = await response.Content.ReadAsStringAsync();
            if (checkedVersion != Application.ProductVersion & BetaCheck() == false)
            {
                var result = MessageBox.Show($"Please update to Version {checkedVersion}. Current version: {Application.ProductVersion}.", "Desktop Support App: Alert", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
                if (result == DialogResult.OK)
                {
                    HTTP.OpenURL(Globals.g_sharepointHome);
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
