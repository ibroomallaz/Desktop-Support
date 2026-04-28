using System.Text.RegularExpressions;

namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class LinksUiSettings
    {
        public bool OpenLastViewedFirst { get; set; } = true;
        public bool OverrideEnabled { get; set; } = false;
        public string? OverrideTeam { get; set; }
        public string? LastTeam { get; set; }

        public void Normalize()
        {
            OverrideTeam = CleanName(OverrideTeam);
            LastTeam = CleanName(LastTeam);
            if (OverrideEnabled && string.IsNullOrEmpty(OverrideTeam)) OverrideEnabled = false;
        }

        private static readonly Regex s_wsCollapse = new(@"\s+", RegexOptions.Compiled);
        private static string? CleanName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim();
            t = s_wsCollapse.Replace(t, " ");
            return t.Length == 0 ? null : t;
        }
    }
}