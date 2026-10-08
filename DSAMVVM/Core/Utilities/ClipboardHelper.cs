using System.Text;
using System.Windows;
using DSAMVVM.Core.Logging;

namespace DSAMVVM.Core.Utilities
{
    public static class ClipboardHelper
    {
        private const string HeaderTemplate =
            "Version:0.9\r\n" +
            "StartHTML:{0:D8}\r\n" +
            "EndHTML:{1:D8}\r\n" +
            "StartFragment:{2:D8}\r\n" +
            "EndFragment:{3:D8}\r\n";

        private const string HtmlPrefix =
            "<!DOCTYPE html>\r\n<html>\r\n<body>\r\n<!--StartFragment-->";

        private const string HtmlSuffix =
            "<!--EndFragment-->\r\n</body>\r\n</html>";


        public static bool CopyRichAndPlainText(string htmlFragment, string plainText)
        {
            if (string.IsNullOrEmpty(htmlFragment) && string.IsNullOrEmpty(plainText))
                return false;

            try
            {
                var dataObject = new DataObject();

                if (!string.IsNullOrEmpty(plainText))
                {
                    dataObject.SetData(DataFormats.UnicodeText, plainText, true);
                }

                if (!string.IsNullOrEmpty(htmlFragment))
                {
                    string cfHtml = WrapHtmlForClipboard(htmlFragment);
                    dataObject.SetData(DataFormats.Html, cfHtml, true);
                }

                Clipboard.SetDataObject(dataObject, true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("ClipboardHelper", $"Failed to copy rich data to clipboard: {ex.Message}");
                return false;
            }
        }

        public static string WrapHtmlForClipboard(string htmlFragment)
        {
            // Placeholder header with 8-digit zero placeholders to calculate base header length in UTF-8
            string dummyHeader = string.Format(HeaderTemplate, 0, 0, 0, 0);
            int headerByteCount = Encoding.UTF8.GetByteCount(dummyHeader);
            int prefixByteCount = Encoding.UTF8.GetByteCount(HtmlPrefix);
            int fragmentByteCount = Encoding.UTF8.GetByteCount(htmlFragment);
            int suffixByteCount = Encoding.UTF8.GetByteCount(HtmlSuffix);

            int startHtml = headerByteCount;
            int startFragment = headerByteCount + prefixByteCount;
            int endFragment = startFragment + fragmentByteCount;
            int endHtml = endFragment + suffixByteCount;

            string actualHeader = string.Format(HeaderTemplate, startHtml, endHtml, startFragment, endFragment);
            return actualHeader + HtmlPrefix + htmlFragment + HtmlSuffix;
        }
    }
}
