namespace DSAMVVM.MVVM.Model.Config.UI
{
    public sealed class ServiceMeowUiSettings
    {
        // Customizable rotation interval: e.g. "15m", "30m", "1h", "24h", "Daily"
        public string RotationInterval { get; set; } = "24h";

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(RotationInterval))
            {
                RotationInterval = "24h";
            }
        }
    }
}
