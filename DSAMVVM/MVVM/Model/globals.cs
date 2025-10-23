using DSAMVVM.Core.Utilities;
using System.IO;

namespace DSAMVVM.MVVM.Model
{
    public class Globals
    {

        public static string g_AppVersion = VersionDisplayHelper.GetSemVerDisplay();

        // AD constants

        public const string g_domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";

        // JSON locations (remote)
        public const string g_DepartmentJSONURL = "https://arizona.box.com/shared/static/wj9xs1pqsikyya4hkxuyu84dmvm91g4r.json";
        public const string g_VersionJSON = "https://arizona.box.com/shared/static/ccfzlvn1gtfdjxv8n9c63uo68fqckp7n.json";
        public const string g_TestVersionJSON = "https://arizona.box.com/shared/static/rtt7xirnv2heobjf85t11em8gg6hburt.json";
        public const string g_LinksJSON = "https://arizona.box.com/shared/static/zg9sd4zpbfse7vk060e4fsegqabhdacs.json";
        public const string g_NewsJSON = "https://arizona.box.com/shared/static/z3nbs9pyehw27len70mhz0p71q4xxidz.json";

        // Links
        public const string g_SharepointHome = "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/ProjectHome.aspx";
        public const string g_GitHubUrl = "https://github.com/ibroomallaz/Desktop-Support";
        public const string g_ChangeLogURL = "https://emailarizona.sharepoint.com/sites/TLC-desktopsupportapp/SitePages/Change-Log.aspx";

        // App directories
        public static readonly string g_AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "UArizona", "DesktopSupportApp");
        public static readonly string g_DataDir = Path.Combine(g_AppDir, "data");

        // Settings file (local)
        public const string g_SettingsFileName = "settings.json";
        public static readonly string g_SettingsPath = Path.Combine(g_AppDir, g_SettingsFileName);

        // Backup/cache files (local)
        public static readonly string g_DepartmentCachePath = Path.Combine(g_DataDir, "departments.json");
        public static readonly string g_LinksCachePath = Path.Combine(g_DataDir, "links.json");

        // Logs + legacy settings dirs
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
            EnsureDirSafe(g_DataDir);
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

        // If a file exists where our folder should be, this is fatal
        private static void EnsureDirSafe(string path)
        {
            if (File.Exists(path))
                throw new IOException($"A file exists where a directory is expected: {path}");
            Directory.CreateDirectory(path);
        }
    }
}
