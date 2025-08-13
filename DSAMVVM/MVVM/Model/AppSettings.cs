using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DSAMVVM.MVVM.Model
{
    // Centralized UI limits/helpers
   

    public class AppSettings
    {
        public Meta Meta { get; set; } = new();
        public Paths Paths { get; set; } = new();
        public Ui Ui { get; set; } = new();

        // Call after deserialization
        public void ApplyDefaultsAndClamp()
        {
            Ui?.Search?.Clamp();
            Ui?.Font?.Clamp();

            if (Ui?.ViewFontSizes != null)
            {
                foreach (var kvp in Ui.ViewFontSizes)
                    kvp.Value?.Clamp();
            }
        }
    }

    public class Meta
    {
        public int SchemaVersion { get; set; } = 1;
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
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
    }

    public class Ui
    {
        public FontSettings Font { get; set; } = new();
        public Dictionary<string, ViewFontSetting> ViewFontSizes { get; set; } = new();
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
        public const double MaxFontSize = 48.0;

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
}
