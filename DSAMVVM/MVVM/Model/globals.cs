using System;
using System.Reflection;
using System.Windows;
using static System.Net.WebRequestMethods;

namespace DSAMVVM.MVVM.Model
{
    public class Globals
    {
        public static string g_AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        public static IDepartmentService DepartmentService { get; set; } = new DepartmentService();
        public const string g_domainPath = "bluecat.arizona.edu";
        public const string g_domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";
        public const string g_prodURL = "https://arizona.box.com/shared/static/27qy9jc64b0cpz4l6zzeu8pnri65y4d0.csv";
        const string g_testURL = "https://arizona.box.com/shared/static/osspbuwb9bgqknom1ts1uk2c173xgn5k.csv";
        public const string g_QuickLinksURL = "https://arizona.box.com/shared/static/4jonapcgzw5lq2i8m40doma5x9t684de.json";
        public const string g_versionTxt = "https://arizona.box.com/shared/static/5o9izr016qh0ywr8hsdk2f7vkijdl0xv.txt";
        public const string g_sharepointHome = "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx";
        public const string g_DepartmentJSONURL = "https://arizona.box.com/shared/static/j3w4j5gdhhden2dheuthu2sdhunp2oxl.json";
        public const string g_versionJSON = "https://arizona.box.com/shared/static/ccfzlvn1gtfdjxv8n9c63uo68fqckp7n.json";
    }
}
