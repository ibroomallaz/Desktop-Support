namespace DSAMVVM.Core.Interfaces.Graph
{
    public interface ITeamsMessagePayload
    {
        string TeamId { get; }
        string ChannelId { get; }
        string GetSubject();
        string GetHtmlBody();
    }
}