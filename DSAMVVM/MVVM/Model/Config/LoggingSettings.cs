using DSAMVVM.Core.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DSAMVVM.MVVM.Model.Config
{
    public sealed class LoggingSettings
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AppLogLevel MinimumLevel { get; set; } = AppLogLevel.Warn;
        public int RetentionDays { get; set; } = 14;

        public void Clamp()
        {
            if (RetentionDays < 1) RetentionDays = 1;
            if (!Enum.IsDefined(MinimumLevel)) MinimumLevel = AppLogLevel.Warn;
        }
    }
}