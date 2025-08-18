using System;
using System.IO;
using System.Reflection;

namespace DSAMVVM.MVVM.Model
{
    public class Globals
    {
        // App version from assembly info (fallback to 4.0.0)
        private static readonly string v =
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "4.0.0";
        public static string g_AppVersion = v;

        // AD constants
        public const string g_domainPath = "bluecat.arizona.edu";
        public const string g_domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";

        // JSON Location constants
        public const string g_QuickLinksURL = "https://arizona.box.com/shared/static/4jonapcgzw5lq2i8m40doma5x9t684de.json"; // old
        public const string g_DepartmentJSONURL = "https://arizona.box.com/shared/static/wj9xs1pqsikyya4hkxuyu84dmvm91g4r.json";
        public const string g_versionJSON = "https://arizona.box.com/shared/static/ccfzlvn1gtfdjxv8n9c63uo68fqckp7n.json";
        public const string g_testVersionJSON = "https://arizona.box.com/shared/static/rtt7xirnv2heobjf85t11em8gg6hburt.json";
        public const string g_LinksJSON = "https://arizona.box.com/shared/static/zg9sd4zpbfse7vk060e4fsegqabhdacs.json";

        // SharePoint home
        public const string g_sharepointHome = "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx";

        // App directories
        public static readonly string g_AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "UArizona", "DesktopSupportApp");

        // Logs + legacy settings dirs under the app dir
        public static readonly string g_LogsDir = Path.Combine(g_AppDir, "logs");
        public static readonly string g_SettingsLegacyDir = Path.Combine(g_AppDir, "settings-legacy");

        // Schema versions
        public const int g_SettingsSchema = 1;
        public const int g_DepartmentJSONSchema = 2;
        public const int g_LinkJSONSchema = 2;
        public const int g_VersionSchema = 2;

        // Ensure the core directories exist (throws if a file blocks a folder path)
        public static void EnsureCoreDirs()
        {
            EnsureDirSafe(g_AppDir);
            EnsureDirSafe(g_LogsDir);
            EnsureDirSafe(g_SettingsLegacyDir);
        }

        // Best-effort creation; returns false and sets error on failure
        public static bool TryEnsureCoreDirs(out string? error)
        {
            try
            {
                EnsureCoreDirs();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // If a files exists where our folder should be, this is fatal
        private static void EnsureDirSafe(string path)
        {
            if (File.Exists(path))
                throw new IOException($"A file exists where a directory is expected: {path}");
            Directory.CreateDirectory(path);
        }
    }
}
