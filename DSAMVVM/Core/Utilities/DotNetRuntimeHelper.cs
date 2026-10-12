using System.IO;

namespace DSAMVVM.Core.Utilities
{
    public static class DotNetRuntimeHelper
    {
        private const string WindowsDesktopRuntimePath = @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App";

        /// <summary>
        /// Checks whether the target .NET Desktop Runtime version is present on the machine.
        /// </summary>
        /// <param name="requiredVersion">The required version string, or null/empty if no specific version is required.</param>
        /// <returns><c>true</c> if the required runtime is present or not specified; otherwise, <c>false</c>.</returns>
        public static bool IsTargetRuntimePresent(string? requiredVersion)
        {
            // If no version is specified in the JSON, we assume no change
            if (string.IsNullOrWhiteSpace(requiredVersion)) return true;
            if (!Version.TryParse(requiredVersion, out var required)) return true;

            if (!Directory.Exists(WindowsDesktopRuntimePath)) return false;

            // Check all installed versions in the shared folder
            return Directory.GetDirectories(WindowsDesktopRuntimePath)
                .Select(Path.GetFileName)
                .Any(name => Version.TryParse(name, out var installed) && installed >= required);
        }
    }
}
