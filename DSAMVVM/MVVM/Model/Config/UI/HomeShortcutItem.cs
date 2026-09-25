using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.View.Resources;
using Newtonsoft.Json;
using System.Windows.Input;

namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class HomeShortcutItem : ObservableObject
    {
        private string _id = Guid.NewGuid().ToString("N")[..8];
        public string Id
        {
            get => _id;
            set => Set(ref _id, value);
        }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        private string _target = string.Empty;
        public string Target
        {
            get => _target;
            set => Set(ref _target, value);
        }

        private string _icon = Glyphs.Links;
        public string Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }

        private string _colorPreset = "Blue";
        public string ColorPreset
        {
            get => _colorPreset;
            set => Set(ref _colorPreset, ShortcutColorPresets.Normalize(value));
        }

        private bool _isCustom;
        public bool IsCustom
        {
            get => _isCustom;
            set => Set(ref _isCustom, value);
        }

        private int _order;
        public int Order
        {
            get => _order;
            set => Set(ref _order, value);
        }

        [JsonIgnore]
        public ICommand? OpenCommand { get; set; }

        public HomeShortcutItem Clone() => new()
        {
            Id = Id,
            Title = Title,
            Description = Description,
            Target = Target,
            Icon = Icon,
            ColorPreset = ColorPreset,
            IsCustom = IsCustom,
            Order = Order
        };
    }

    public static class ShortcutColorPresets
    {
        public const string Blue = "Blue";
        public const string Amber = "Amber";
        public const string Green = "Green";
        public const string Rose = "Rose";
        public const string Purple = "Purple";
        public const string Teal = "Teal";

        public static readonly IReadOnlyList<string> Presets =
        [
            Blue,
            Amber,
            Green,
            Rose,
            Purple,
            Teal
        ];

        public static string Normalize(string? preset)
        {
            if (string.IsNullOrWhiteSpace(preset)) return Blue;
            return Presets.FirstOrDefault(p => string.Equals(p, preset, StringComparison.OrdinalIgnoreCase)) ?? Blue;
        }
    }

    public sealed record ShortcutGlyphOption(string Glyph, string Name);

    public static class ShortcutGlyphs
    {
        public static readonly IReadOnlyList<ShortcutGlyphOption> Options =
        [
            new(Glyphs.Ticket, "Ticket / Service"),
            new(Glyphs.Key, "Key / Security"),
            new(Glyphs.Links, "Web / External"),
            new(Glyphs.Server, "Docs / Knowledge"),
            new(Glyphs.Settings, "Settings"),
            new(Glyphs.Star, "Favorite"),
            new(Glyphs.User, "User / Identity"),
            new(Glyphs.Computer, "Device / Workstation"),
            new(Glyphs.Entra, "Cloud / Directory"),
            new(Glyphs.Terminal, "Terminal / CLI"),
            new(Glyphs.Support, "Phone / Support"),
            new(Glyphs.Checklist, "Checklist / Tasks")
        ];
    }
}
