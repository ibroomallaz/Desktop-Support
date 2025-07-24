using DSAMVVM.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace DSAMVVM.MVVM.Model.utils
{
    public class HTTPService
    {
        private static readonly HttpClient client = new();

        public static async Task DownloadFile(string url, string fileName)
        {
            using var s = await client.GetStreamAsync(url);
            using var fs = new FileStream(fileName, FileMode.Create);
            await s.CopyToAsync(fs);
        }


        //TODO: Change to statusbar method
        public static void OpenURL(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo() { FileName = target, UseShellExecute = true });
                // Replace with status bar reporting later
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

    }
}
