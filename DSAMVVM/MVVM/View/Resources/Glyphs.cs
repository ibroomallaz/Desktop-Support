namespace DSAMVVM.MVVM.View.Resources
{
    // Central glyph hub for the app. Only NAV set is populated for now.
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

        // ===== TOP =====
        public const string Warning = "\uE7BA";


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
