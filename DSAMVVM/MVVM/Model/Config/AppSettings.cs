using DSAMVVM.Core.Enums;
using DSAMVVM.MVVM.Model.Schemas;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DSAMVVM.MVVM.Model.Config
{
    public class AppSettings
    {
        public SettingsMeta Meta { get; set; } = new();
        public Paths Paths { get; set; } = new();
        public Ui Ui { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        // Call after deserialization
        public void ApplyDefaultsAndClamp()
        {
            // ensure Meta.LastUpdatedUtc is set when missing
            Meta?.Normalize();

            Ui?.Search?.Clamp();
            Ui?.Font?.Clamp();

            // Ensure case-insensitive view keys even if JSON replaced the dictionary
            if (Ui?.ViewFontSizes != null &&
                !ReferenceEquals(Ui.ViewFontSizes.Comparer, StringComparer.OrdinalIgnoreCase))
            {
                Ui.ViewFontSizes = new Dictionary<string, ViewFontSetting>(Ui.ViewFontSizes, StringComparer.OrdinalIgnoreCase);
            }

            if (Ui?.ViewFontSizes != null)
            {
                foreach (var kvp in Ui.ViewFontSizes)
                    kvp.Value?.Clamp();
            }

            // Normalize data locations
            Paths?.DepartmentData?.Normalize();
            Paths?.LinksData?.Normalize();

            // Clamp logging settings
            Logging?.Clamp();
        }
    }

    public class Paths
    {
        public string DataDir { get; set; } = "data";
        public DataLocation DepartmentData { get; set; } = new();
        public DataLocation LinksData { get; set; } = new();
    }

    public class DataLocation
    {
        public string Source { get; set; } = "web"; // "web" | "file"
        public string Uri { get; set; } = "";       // URL or path
        public string? FallbackFile { get; set; }   // relative to DataDir if not absolute

        public void Normalize()
        {
            // Force Source to "web" or "file" only
            if (!Source.Equals("file", StringComparison.OrdinalIgnoreCase) &&
                !Source.Equals("web", StringComparison.OrdinalIgnoreCase))
            {
                Source = "web";
            }

            // Null-safe fields
            Uri ??= "";
            if (string.IsNullOrWhiteSpace(FallbackFile)) FallbackFile = null;
        }
    }

    public class Ui
    {
        public FontSettings Font { get; set; } = new();

        // Case-insensitive keys so "UserView" and "userview" don't duplicate
        public Dictionary<string, ViewFontSetting> ViewFontSizes { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        public SearchSettings Search { get; set; } = new();
    }

    public class FontSettings
    {
        public double DefaultSize { get; set; } = 14.0;
        public bool ViewFontSizeOverride { get; set; } = false;

        public void Clamp()
        {
            DefaultSize = UiLimits.ClampFontSize(DefaultSize);
        }
    }

    public class ViewFontSetting
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
            if (size < MinFontSize) return MinFontSize;
            if (size > MaxFontSize) return MaxFontSize;
            return size;
        }
    }

    public class SearchSettings
    {
        public bool UseSavedSearchHistory { get; set; } = true;
        public int MaxSearchHistory { get; set; } = 10; // default

        [JsonIgnore] public const int Min = 5;
        [JsonIgnore] public const int Max = 25;

        public void Clamp()
        {
            if (MaxSearchHistory < Min) MaxSearchHistory = Min;
            if (MaxSearchHistory > Max) MaxSearchHistory = Max;
        }
    }

    public class LoggingSettings
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AppLogLevel MinimumLevel { get; set; } = AppLogLevel.Warn;

        public int RetentionDays { get; set; } = 14;

        public void Clamp()
        {
            if (RetentionDays < 1) RetentionDays = 1;
            if (!Enum.IsDefined(typeof(AppLogLevel), MinimumLevel))
                MinimumLevel = AppLogLevel.Warn;
        }
    }
}
