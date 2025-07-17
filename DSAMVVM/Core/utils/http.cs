using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DSAMVVM.Core.utils
{
    public static class HTTP
    {
        public static async Task DownloadFile(string url, string FileName)
        {
            HttpClient client = new HttpClient();
            var uri = new Uri(url);
            using (var s = await client.GetStreamAsync(uri))
            {
                using (var fs = new FileStream(FileName, FileMode.Create))
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
}
