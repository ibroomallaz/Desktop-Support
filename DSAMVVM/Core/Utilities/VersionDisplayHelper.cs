using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DSAMVVM.Core.Utilities
{
    public static class VersionDisplayHelper
    {
        // keeps prerelease, drops +build metadata
        private static readonly Regex SemVerRx = new(@"^(?:v)?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?", RegexOptions.Compiled);

        public static string GetSemVerDisplay(bool lowercase = true)
        {
            var raw = GetRawInformational();
            var noBuild = StripAfterPlus(raw);               // "1.2.3-rc+abcd" -> "1.2.3-rc"
            var m = SemVerRx.Match(noBuild);
            var s = m.Success ? TrimV(noBuild[..m.Length]) : FallbackFromFileVersion();
            return lowercase ? s.ToLowerInvariant() : s;
        }

        public static string GetSemVerCore(bool lowercase = true)
        {
            var s = GetSemVerDisplay(false);
            var dash = s.IndexOf('-');
            s = dash >= 0 ? s[..dash] : s;                   // "1.2.3-rc" -> "1.2.3"
            return lowercase ? s.ToLowerInvariant() : s;
        }

        private static string GetRawInformational()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? FallbackFromFileVersion();
        }

        private static string StripAfterPlus(string s) { var i = s.IndexOf('+'); return i >= 0 ? s[..i] : s; }
        private static string TrimV(string s) => s.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? s[1..] : s;

        private static string FallbackFromFileVersion()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var fv = FileVersionInfo.GetVersionInfo(asm.Location).FileVersion ?? "1.0.0.0";
            var parts = fv.Split('.');                       // "4.6.0.1234" -> "4.6.0"
            return parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : fv;
        }
    }
}
