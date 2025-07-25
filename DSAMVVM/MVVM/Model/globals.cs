using System;
using System.Reflection;
using System.Windows;
using static System.Net.WebRequestMethods;

namespace DSAMVVM.MVVM.Model
{
    public class Globals
    {
        public static string g_AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "4.0.0";
        public const string g_domainPath = "bluecat.arizona.edu";
        public const string g_domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";
        public const string g_QuickLinksURL = "https://arizona.box.com/shared/static/4jonapcgzw5lq2i8m40doma5x9t684de.json";
        public const string g_sharepointHome = "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx";
        public const string g_DepartmentJSONURL = "https://arizona.box.com/shared/static/j3w4j5gdhhden2dheuthu2sdhunp2oxl.json";
        public const string g_versionJSON = "https://arizona.box.com/shared/static/ccfzlvn1gtfdjxv8n9c63uo68fqckp7n.json";
        public const string g_testVersionJSON = "https://arizona.box.com/shared/static/rtt7xirnv2heobjf85t11em8gg6hburt.json";
        public const string g_LinksJSON = "https://arizona.box.com/shared/static/zg9sd4zpbfse7vk060e4fsegqabhdacs.json";
    }
}
