using DSAMVVM.Core.Interfaces;
using Microsoft.Win32;

namespace DSAMVVM.Core.Services
{
    public class FileDialogService : IFileDialogService
    {
        public string? SelectFolder(string? description = null, string? initialDirectory = null)
        {
            var dialog = new OpenFolderDialog
            {
                Title = description ?? "Select Destination Folder",
                InitialDirectory = string.IsNullOrWhiteSpace(initialDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : initialDirectory
            };

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }

        public string? SaveFile(string defaultFileName, string filter, string? title = null, string? initialDirectory = null)
        {
            var dialog = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = filter,
                Title = title ?? "Save File",
                InitialDirectory = string.IsNullOrWhiteSpace(initialDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : initialDirectory,
                DefaultExt = ".json",
                AddExtension = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? OpenFile(string filter, string? title = null, string? initialDirectory = null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title ?? "Select File",
                InitialDirectory = string.IsNullOrWhiteSpace(initialDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : initialDirectory,
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
