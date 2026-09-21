namespace DSAMVVM.Core.Interfaces
{
    public interface IFileDialogService
    {
        string? SelectFolder(string? description = null, string? initialDirectory = null);
        string? SaveFile(string defaultFileName, string filter, string? title = null, string? initialDirectory = null);
        string? OpenFile(string filter, string? title = null, string? initialDirectory = null);
    }
}
