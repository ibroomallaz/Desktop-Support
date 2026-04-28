using Newtonsoft.Json;

namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class SearchSettings
    {
        public bool UseSavedSearchHistory { get; set; } = true;
        public int MaxSearchHistory { get; set; } = 10;

        [JsonIgnore] public const int Min = 5;
        [JsonIgnore] public const int Max = 25;

        public void Clamp()
        {
            if (MaxSearchHistory < Min) MaxSearchHistory = Min;
            if (MaxSearchHistory > Max) MaxSearchHistory = Max;
        }
    }
}