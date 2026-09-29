using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Interfaces
{
    public interface INetworkDetectionService : IDisposable
    {
        NetworkStateInfo CurrentState { get; }
        event EventHandler<NetworkStateInfo>? NetworkStateChanged;

        NetworkStateInfo Refresh();
        Task<NetworkStateInfo> RefreshAsync();
    }
}
