namespace DSAMVVM.Core.Interfaces
{
    public interface IReportService
    {
        // Submits a contextual data discrepancy report to the Support Team Update channel.
        Task<bool> SubmitDataDiscrepancyAsync(string targetUser, string incorrectGroup, string reporter);

        // Submits an application-level bug report to the bug channel.
        Task<bool> SubmitBugReportAsync(string reporter, string appVersion, string userComments);

        // Submits a feature request to the Request channel.
        Task<bool> SubmitFeatureRequestAsync(string reporter, string appVersion, string requestDetails);
    }
}