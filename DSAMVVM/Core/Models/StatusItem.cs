namespace DSAMVVM.Core.Models
{
    public enum StatusLevel { Info, Success, Warning, Error }

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

    public record StatusItem(
        string Key,
        StatusLevel Level,
        bool Sticky,
        int Priority,
        IReadOnlyList<StatusSpan> Spans
    );
}
