using DSAMVVM.Core.Utilities;
using System.IO;

namespace DSAMVVM.MVVM.Model
{
    public abstract class Globals
    {
#pragma warning disable CA2211 // Non-constant fields should not be visible
        public static readonly string g_AppVersion = VersionDisplayHelper.GetSemVerDisplay();
        public static readonly string g_FileVersion = VersionDisplayHelper.GetFileVersionDisplay();
#pragma warning restore CA2211 // Non-constant fields should not be visible

        //Entra ID / Graph constants
        //Publicly discoverable keys are safe for repository
        public const string EntraClientId = "36cd585b-c3cd-48e0-9fef-0a5fc69f0fa0";
        public const string EntraTenantId = "5ee35505-eb8e-4929-937d-645df5013288";
        public const string EntraInstanceUrl = "https://login.microsoftonline.com/";
        public const string EntraRedirectUri = "dsa://auth";

        // AD constants

        public const string g_domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";

        // JSON locations (remote)
        public const string g_DepartmentJSONURL = "https://arizona.box.com/shared/static/wj9xs1pqsikyya4hkxuyu84dmvm91g4r.json";
        public const string g_VersionJSON = "https://arizona.box.com/shared/static/ccfzlvn1gtfdjxv8n9c63uo68fqckp7n.json";
        public const string g_TestVersionJSON = "https://arizona.box.com/shared/static/rtt7xirnv2heobjf85t11em8gg6hburt.json";
        public const string g_LinksJSON = "https://arizona.box.com/shared/static/zg9sd4zpbfse7vk060e4fsegqabhdacs.json";
        public const string g_NewsJSON = "https://arizona.box.com/shared/static/z3nbs9pyehw27len70mhz0p71q4xxidz.json";
        public const string g_DepartmentTestJSONURL = "https://arizona.box.com/shared/static/nuiy4gqgxqwzlnzzz893id5bnmal2f86.json";
        public const string g_ServiceMeowJSON = "https://arizona.box.com/shared/static/n0w81iie8a9okuq85u3zh55gp1rfr6xx.json";

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

        // Backup/cache files and directories (local)
        public static readonly string g_DepartmentCachePath = Path.Combine(g_DataDir, "departments.json");
        public static readonly string g_LinksCachePath = Path.Combine(g_DataDir, "links.json");
        public static readonly string g_ServiceMeowCachePath = Path.Combine(g_DataDir, "servicemeow.json");
        // Images directory (local)
        public static readonly string g_ImgDir = Path.Combine(g_AppDir, "img");
        public static readonly string g_ServiceMeowImageCacheDir = Path.Combine(g_ImgDir, "sm");

        // Logs + legacy settings dirs
        public static readonly string g_LogsDir = Path.Combine(g_AppDir, "logs");
        public static readonly string g_SettingsLegacyDir = Path.Combine(g_AppDir, "settings-legacy");

        // Schema versions
        public const int g_SettingsSchema = 4;
        public const int g_DepartmentJSONSchema = 3;
        public const int g_LinkJSONSchema = 2;
        public const int g_VersionSchema = 3;
        public const int g_ServiceMeowJSONSchema = 1;
    }
}
