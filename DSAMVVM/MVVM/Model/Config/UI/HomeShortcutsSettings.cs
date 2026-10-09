using DSAMVVM.MVVM.View.Resources;

namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class HomeShortcutsSettings
    {
        public const int MaxShortcuts = 6;

        public List<HomeShortcutItem> Items { get; set; } = [];

        public void Normalize()
        {
            switch (Items.Count)
            {
                case 0:
                    Items = GetDefaultShortcuts();
                    return;
                case > MaxShortcuts:
                    Items = Items.Take(MaxShortcuts).ToList();
                    break;
            }

            var defaults = GetDefaultShortcuts();
            var defaultsById = defaults.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

            // Detect legacy condition where all items collapsed to the same color preset (e.g. all Blue during migration)
            bool allUniformColor = Items.Count > 1 &&
                                   Items.All(x => string.Equals(x.ColorPreset, Items[0].ColorPreset, StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                item.Order = i;

                if (string.IsNullOrWhiteSpace(item.Id))
                    item.Id = Guid.NewGuid().ToString("N")[..8];

                if (defaultsById.TryGetValue(item.Id, out var def))
                {
                    // Restore glyph if it became '?' due to legacy ASCII serialization glitch
                    if (string.IsNullOrWhiteSpace(item.Icon) || item.Icon == "?")
                        item.Icon = def.Icon;

                    // Restore rich jewel-tone default color if items collapsed to uniform
                    if (!item.IsCustom && allUniformColor)
                    {
                        item.ColorPreset = def.ColorPreset;
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(item.Icon) || item.Icon == "?")
                        item.Icon = Glyphs.Links;

                    if (allUniformColor && item.IsCustom)
                    {
                        item.ColorPreset = ShortcutColorPresets.Presets[i % ShortcutColorPresets.Presets.Count];
                    }
                }

                if (string.IsNullOrWhiteSpace(item.Title))
                    item.Title = "Shortcut";

                item.ColorPreset = ShortcutColorPresets.Normalize(item.ColorPreset);
            }
        }

        public static List<HomeShortcutItem> GetDefaultShortcuts() =>
        [
            new()
            {
                Id = "s-now",
                Icon = Glyphs.Ticket,
                Title = "ServiceNow",
                Description = "Incident & request queue",
                Target = "https://uarizona.service-now.com/",
                ColorPreset = ShortcutColorPresets.Blue,
                IsCustom = false,
                Order = 0
            },
            new()
            {
                Id = "netid",
                Icon = Glyphs.Key,
                Title = "NetID Portal",
                Description = "Password reset & 2FA tools",
                Target = "https://netid.arizona.edu",
                ColorPreset = ShortcutColorPresets.Amber,
                IsCustom = false,
                Order = 1
            },
            new()
            {
                Id = "status",
                Icon = Glyphs.Links,
                Title = "IT Status",
                Description = "Campus outage dashboard",
                Target = "https://it.arizona.edu/status",
                ColorPreset = ShortcutColorPresets.Green,
                IsCustom = false,
                Order = 2
            },
            new()
            {
                Id = "kb",
                Icon = Glyphs.Server,
                Title = "Knowledge Base",
                Description = "UITS Knowledge Base",
                Target = "https://uarizona.service-now.com/sp?id=kb_view2",
                ColorPreset = ShortcutColorPresets.Rose,
                IsCustom = false,
                Order = 3
            }
        ];
    }
}
