using System.Text.RegularExpressions;

namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed partial class LinksUiSettings
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

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceCollapseRegex();

        private static string? CleanName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim();

            t = WhitespaceCollapseRegex().Replace(t, " ");

            return t.Length == 0 ? null : t;
        }
    }
}