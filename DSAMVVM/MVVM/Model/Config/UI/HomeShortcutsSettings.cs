using DSAMVVM.MVVM.View.Resources;

namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class HomeShortcutsSettings
    {
        public const int MaxShortcuts = 6;

        public List<HomeShortcutItem> Items { get; set; } = [];

        public void Normalize()
        {
            if (Items == null || Items.Count == 0)
            {
                Items = GetDefaultShortcuts();
                return;
            }

            if (Items.Count > MaxShortcuts)
            {
                Items = Items.Take(MaxShortcuts).ToList();
            }

            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].Order = i;
                if (string.IsNullOrWhiteSpace(Items[i].Id))
                    Items[i].Id = Guid.NewGuid().ToString("N")[..8];
                if (string.IsNullOrWhiteSpace(Items[i].Title))
                    Items[i].Title = "Shortcut";
                if (string.IsNullOrWhiteSpace(Items[i].Icon))
                    Items[i].Icon = Glyphs.Links;
                Items[i].ColorPreset = ShortcutColorPresets.Normalize(Items[i].ColorPreset);
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
                Target = "https://service-now.arizona.edu",
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
                Description = "Desktop Support SOPs",
                Target = "app://links",
                ColorPreset = ShortcutColorPresets.Rose,
                IsCustom = false,
                Order = 3
            }
        ];
    }
}
