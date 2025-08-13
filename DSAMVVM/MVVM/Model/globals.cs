using System;
using System.IO;
using System.Reflection;

namespace DSAMVVM.MVVM.Model
{
    public static class Globals
    {
        // Version
        private static readonly string v =
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "4.0.0";
        public static string g_AppVersion = v;

        // AD constants
        public const string g_domainPath = "bluecat.arizona.edu";
        public const string g_domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";

        // JSON Location constants
        public const string g_QuickLinksURL = "https://arizona.box.com/shared/static/4jonapcgzw5lq2i8m40doma5x9t684de.json";
        public const string g_DepartmentJSONURL = "https://arizona.box.com/shared/static/j3w4j5gdhhden2dheuthu2sdhunp2oxl.json";
        public const string g_versionJSON = "https://arizona.box.com/shared/static/ccfzlvn1gtfdjxv8n9c63uo68fqckp7n.json";
        public const string g_testVersionJSON = "https://arizona.box.com/shared/static/rtt7xirnv2heobjf85t11em8gg6hburt.json";
        public const string g_LinksJSON = "https://arizona.box.com/shared/static/zg9sd4zpbfse7vk060e4fsegqabhdacs.json";

        // SharePoint
        public const string g_sharepointHome = "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx";

        // App folders/files
        public static readonly string g_AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "UArizona", "DesktopSupportApp");

        public static readonly string g_LogsDir = Path.Combine(g_AppDir, "logs");
        public static readonly string g_SettingsFile = Path.Combine(g_AppDir, "settings.json");
        public static readonly string g_SettingsLegacyDir = Path.Combine(g_AppDir, "settings-legacy");

        // Schemas
        public const int g_SettingsSchema = 1;
        public const int g_DepartmentJSONSchema = 2;
        public const int g_LinkJSONSchema = 2;
        public const int g_VersionSchema = 2;

        // Call once early in App.OnStartup
        public static void EnsureCoreDirs()
        {
            // If a file exists where folder should be
            if (File.Exists(g_AppDir))
                throw new IOException($"A file exists at the app directory path: {g_AppDir}");

            Directory.CreateDirectory(g_AppDir);
            Directory.CreateDirectory(g_LogsDir);
            Directory.CreateDirectory(g_SettingsLegacyDir);
        }
    }
}
