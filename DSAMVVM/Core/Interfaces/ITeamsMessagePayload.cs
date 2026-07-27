namespace DSAMVVM.Core.Interfaces
{
    public interface ITeamsMessagePayload
    {
        string TeamId { get; }
        string ChannelId { get; }
        string GetSubject();
        string GetHtmlBody();
    }
}