using System;

public class Globals
{
    public static readonly string g_domainPath = "bluecat.arizona.edu";
    public static readonly string g_domainPathLDAP = "LDAP://DC=bluecat,DC=arizona,DC=edu";
    public const string g_prodURL = "https://arizona.box.com/shared/static/27qy9jc64b0cpz4l6zzeu8pnri65y4d0.csv";
    const string g_testURL = "https://arizona.box.com/shared/static/osspbuwb9bgqknom1ts1uk2c173xgn5k.csv";
    const string g_QuickLinksURL = "https://arizona.box.com/shared/static/4jonapcgzw5lq2i8m40doma5x9t684de.json";
    public string g_SavePath = Environment.GetEnvironmentVariable("LocalAppData") + @"\Desktop_Support_App\";
}
