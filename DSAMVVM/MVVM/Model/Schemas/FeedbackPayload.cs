namespace DSAMVVM.MVVM.Model.Schemas
{
    public sealed record FeedbackPayload(
        string FeedbackType,            // "Wrong Support Group", "Suggestion", or "Bug"
        string Details,                 // User-provided description or request

        // Contextual fields for "Wrong Support Group"
        string? CurrentSupportGroup,
        string? TargetNetId,
        string? ExpectedSupportGroup,

        // Contextual fields for "Bug"
        string? CurrentViewContext,
        string? AppVersion,
        string? ErrorOutput
    );
}