namespace DSAMVVM.MVVM.Model.Config
{
    public sealed class UpdateSettings
    {
        public bool EnablePreReleaseChannel { get; set; } = false;
        public bool UseInternalTestingSources { get; set; } = false;
    }
}