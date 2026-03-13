using System.Text.RegularExpressions;
using DSAMVVM.Core.Enums;
using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DSAMVVM.MVVM.Model.Config
{
    public sealed class AppSettings
    {
        public SettingsMeta Meta { get; set; } = new();
        public Paths Paths { get; set; } = new();
        public Ui Ui { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
        public UpdateSettings Updates { get; set; } = new();

        // Call after deserialization
        public void ApplyDefaultsAndClamp()
        {
            Meta?.Normalize();

            Ui?.Search?.Clamp();
            Ui?.Font?.Clamp();
            Ui?.Tray?.Normalize();

            // Ensure case-insensitive view keys even if JSON replaced the dictionary
            if (Ui?.ViewFontSizes != null &&
                !ReferenceEquals(Ui.ViewFontSizes.Comparer, StringComparer.OrdinalIgnoreCase))
            {
                Ui.ViewFontSizes = new Dictionary<string, ViewFontSetting>(
                    Ui.ViewFontSizes,
                    StringComparer.OrdinalIgnoreCase);
            }

            if (Ui?.ViewFontSizes != null)
            {
                foreach (var kvp in Ui.ViewFontSizes)
                {
                    kvp.Value?.Clamp();
                }
            }

            // Normalize Links preferences
            Ui?.Links?.Normalize();

            Paths?.DepartmentData?.Normalize();
            Paths?.LinksData?.Normalize();

            Logging?.Clamp();
        }
    }

    public sealed class Paths
    {
        public string DataDir { get; set; } = "data";
        public DataLocation DepartmentData { get; set; } = new();
        public DataLocation LinksData { get; set; } = new();
    }

    public sealed class DataLocation
    {
        // Master toggle for this override
        public bool UseCustomSource { get; set; } = false;

        // "web" | "file"
        public string Source { get; set; } = "web";

        // URL or path
        public string Uri { get; set; } = string.Empty;

        // Relative to DataDir if not absolute
        public string? FallbackFile { get; set; }

        public void Normalize()
        {
            if (!Source.Equals("file", StringComparison.OrdinalIgnoreCase) &&
                !Source.Equals("web", StringComparison.OrdinalIgnoreCase))
            {
                Source = "web";
            }

            Uri ??= string.Empty;
            if (string.IsNullOrWhiteSpace(FallbackFile))
            {
                FallbackFile = null;
            }

            // Safety: If enabled but no URI provided, auto-disable to prevent errors
            if (UseCustomSource && string.IsNullOrWhiteSpace(Uri))
            {
                UseCustomSource = false;
            }
        }
    }

    public sealed class Ui
    {
        public FontSettings Font { get; set; } = new();

        // Case-insensitive keys so "UserView" and "userview" don't duplicate
        public Dictionary<string, ViewFontSetting> ViewFontSizes { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public SearchSettings Search { get; set; } = new();

        // Links preferences
        public LinksUiSettings Links { get; set; } = new();

        // System Tray preferences
        public TrayUiSettings Tray { get; set; } = new();
    }

    public sealed class TrayUiSettings
    {
        public bool EnableTrayIcon { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool CloseToTray { get; set; } = false;

        public void Normalize()
        {
            // Safety: If the tray is disabled, ensure window hiding flags are disabled
            // so the app doesn't hide itself into a non-existent tray.
            if (!EnableTrayIcon)
            {
                MinimizeToTray = false;
                CloseToTray = false;
            }
        }
    }

    public sealed class LinksUiSettings
    {
        // Preferences
        public bool OpenLastViewedFirst { get; set; } = true;
        public bool OverrideEnabled { get; set; } = false;
        public string? OverrideTeam { get; set; }

        // Operational state used when OpenLastViewedFirst is true
        public string? LastTeam { get; set; }

        // Trim/collapse whitespace; disable override if team is empty
        public void Normalize()
        {
            OverrideTeam = CleanName(OverrideTeam);
            LastTeam = CleanName(LastTeam);

            if (OverrideEnabled && string.IsNullOrEmpty(OverrideTeam))
            {
                OverrideEnabled = false;
            }
        }

        private static readonly Regex s_wsCollapse = new(@"\s+", RegexOptions.Compiled);

        private static string? CleanName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            var t = s.Trim();
            t = s_wsCollapse.Replace(t, " ");
            return t.Length == 0 ? null : t;
        }
    }

    public sealed class FontSettings
    {
        public double DefaultSize { get; set; } = 14.0;
        public bool ViewFontSizeOverride { get; set; } = false;

        public void Clamp()
        {
            DefaultSize = UiLimits.ClampFontSize(DefaultSize);
        }
    }

    public sealed class ViewFontSetting
    {
        public double FontSize { get; set; }

        public void Clamp()
        {
            FontSize = UiLimits.ClampFontSize(FontSize);
        }
    }

    public static class UiLimits
    {
        public const double MinFontSize = 8.0;
        public const double MaxFontSize = 24.0;

        public static double ClampFontSize(double size)
        {
            if (size < MinFontSize)
            {
                return MinFontSize;
            }

            if (size > MaxFontSize)
            {
                return MaxFontSize;
            }

            return size;
        }
    }

    public sealed class SearchSettings
    {
        public bool UseSavedSearchHistory { get; set; } = true;
        public int MaxSearchHistory { get; set; } = 10;

        [JsonIgnore] public const int Min = 5;
        [JsonIgnore] public const int Max = 25;

        public void Clamp()
        {
            if (MaxSearchHistory < Min)
            {
                MaxSearchHistory = Min;
            }

            if (MaxSearchHistory > Max)
            {
                MaxSearchHistory = Max;
            }
        }
    }

    public sealed class LoggingSettings
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AppLogLevel MinimumLevel { get; set; } = AppLogLevel.Warn;

        public int RetentionDays { get; set; } = 14;

        public void Clamp()
        {
            if (RetentionDays < 1)
            {
                RetentionDays = 1;
            }

            if (!Enum.IsDefined(MinimumLevel))
            {
                MinimumLevel = AppLogLevel.Warn;
            }
        }
    }
    public sealed class UpdateSettings
    {
        public bool EnablePreReleaseChannel { get; set; } = false;
        public bool UseInternalTestingSources { get; set; } = false;
    }
}