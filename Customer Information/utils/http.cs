using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

public static class HTTP
    {
    public static async Task DownloadFile(Uri uri, string FileName)
    {
        HttpClient client = new HttpClient();
        using (var s = await client.GetStreamAsync(uri))
        {
            using (var fs = new FileStream(FileName, FileMode.CreateNew))
            {
                await s.CopyToAsync(fs);
            }
        }
    }
   public static void OpenURL(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo() { FileName = target, UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception noBrowser)
        {
            if (noBrowser.ErrorCode == -2147467259)
                MessageBox.Show(noBrowser.Message);
        }
        catch (System.Exception other)
        {
            MessageBox.Show(other.Message);
        }
    }
}

