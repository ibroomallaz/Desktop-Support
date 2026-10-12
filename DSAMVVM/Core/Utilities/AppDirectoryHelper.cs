using DSAMVVM.MVVM.Model;
using System.IO;

namespace DSAMVVM.Core.Utilities
{
    public static class AppDirectoryHelper
    {
        /// <summary>
        /// Ensures all core application directories exist on disk.
        /// Throws an <see cref="IOException"/> if an existing file blocks any directory path.
        /// </summary>
        public static void EnsureCoreDirs()
        {
            EnsureDirSafe(Globals.g_AppDir);
            EnsureDirSafe(Globals.g_LogsDir);
            EnsureDirSafe(Globals.g_SettingsLegacyDir);
            EnsureDirSafe(Globals.g_DataDir);
            EnsureDirSafe(Globals.g_ServiceMeowImageCacheDir);
        }

        /// <summary>
        /// Best-effort creation of core application directories.
        /// Returns false and sets <paramref name="error"/> on failure.
        /// </summary>
        public static bool TryEnsureCoreDirs(out string? error)
        {
            try
            {
                EnsureCoreDirs();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Ensures a directory exists, verifying that a file with the same name does not conflict.
        /// </summary>
        public static void EnsureDirSafe(string path)
        {
            if (File.Exists(path))
                throw new IOException($"A file exists where a directory is expected: {path}");
            Directory.CreateDirectory(path);
        }
    }
}
