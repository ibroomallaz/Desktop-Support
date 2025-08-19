namespace DSAMVVM.Core.Interfaces
{
    public interface IOutputTextSettingsProvider
    {
        // null/empty viewName => use default/global size
        double GetFontSize(string? viewName = null);

        event EventHandler? Changed;
        void NotifyChanged();
    }
}
