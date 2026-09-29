using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Interfaces;

public interface IADDetectionService
{
    ADStateInfo CurrentState { get; }
    event Action<ADStateInfo>? StateChanged;
    Task<ADStateInfo> ProbeDomainControllerAsync(CancellationToken ct = default);
}
