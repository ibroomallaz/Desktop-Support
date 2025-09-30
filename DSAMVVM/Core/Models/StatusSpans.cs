namespace DSAMVVM.Core.Models
{
    public static class StatusSpans
    {
        public static StatusSpan Text(string t) => new(t);
        public static StatusSpan Bold(string t) => new(t, Bold: true);
        public static StatusSpan Underline(string t) => new(t, Underline: true);
        public static StatusSpan Colored(string t, string color) => new(t, Color: color);

        // now supports optional tooltip
        public static StatusSpan Link(string t, Uri uri, string? tooltip = null)
            => new(t, Underline: true, Color: "DodgerBlue", ExternalLink: uri, Tooltip: tooltip);

        // now supports optional tooltip
        public static StatusSpan Action(string t, string command, string? arg = null, string? tooltip = null)
            => new(t, Underline: true, Color: "DodgerBlue", Command: command, CommandArg: arg, Tooltip: tooltip);

        // explicit tooltip-only helper (still available)
        public static StatusSpan Tooltip(string t, string tooltip)
            => new(t, Tooltip: tooltip);
    }
}
