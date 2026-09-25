namespace DSAMVVM.MVVM.View.Resources
{
    // Central glyph hub for the app.
    public static class Glyphs
    {
        // ===== NAV =====
        public const string Home = "\uE80F";
        public const string Search = "\uE721";
        public const string User = "\uE77B";
        public const string Computer = "\uE7F8";
        public const string Group = "\uE902";
        public const string Links = "\uE774";
        public const string Settings = "\uE713";
        public const string Entra = "\uE753";
        public const string About = "\uE946";
        public const string Admin = "\uE7EF";

        // ===== TOP / STATUS =====
        public const string Warning = "\uE7BA";
        public const string Server = "\uE82D";
        public const string Sync = "\uE72C";

        // ===== NETWORK =====
        public const string NetworkWired = "\uE839";
        public const string NetworkWiFi = "\uE701";
        public const string NetworkVpn = "\uE72E";
        public const string NetworkOffCampus = "\uE774";
        public const string NetworkDisconnected = "\uEB55";

        // ===== SETTINGS CATEGORIES =====
        public const string Appearance = "\uE790";
        public const string QuickSearch = "\uE721";
        public const string DataAndLinks = "\uE774";
        public const string Maintenance = "\uE90F";

        // ===== ACTIONS & SHORTCUTS =====
        public const string Ticket = "\uE8EC";
        public const string Key = "\uE8D7";
        public const string Star = "\uE734";
        public const string Terminal = "\uE756";
        public const string Support = "\uE719";
        public const string Note = "\uE70F";
        public const string Checklist = "\uE8F1";
        public const string OpenInNew = "\uE8A7";
        public const string Add = "\uE710";
        public const string Dismiss = "\uE711";
        public const string Checkmark = "\uE73E";

        // Optional helper: accept "E721" or "\uE721" and return the single-char glyph.
        public static string From(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            code = code.Trim();
            if (code.StartsWith("\\u")) code = code[2..]; // handles literal "\uE721"
            return int.TryParse(code, System.Globalization.NumberStyles.HexNumber, null, out var v)
                ? char.ConvertFromUtf32(v)
                : code; // fallback if already a single char
        }
    }
}
