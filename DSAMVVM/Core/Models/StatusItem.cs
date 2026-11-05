namespace DSAMVVM.Core.Models
{
    /// <summary>
    /// Severity level classification for status bar notifications.
    /// </summary>
    public enum StatusLevel { Info, Success, Warning, Error }

    /// <summary>
    /// Represents an individual formatted text segment within a status message.
    /// </summary>
    public record StatusSpan(
        string Text,
        bool Bold = false,
        bool Underline = false,
        string? Color = null,
        Uri? ExternalLink = null,
        string? Command = null,
        string? CommandArg = null,
        string? Tooltip = null
    );

    /// <summary>
    /// Represents a notification item dispatched to the application status bar.
    /// </summary>
    public record StatusItem(
        string Key,
        StatusLevel Level,
        bool Sticky,
        int Priority,
        IReadOnlyList<StatusSpan> Spans
    );
}
