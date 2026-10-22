using SinalMap.Domain;

namespace SinalMap.Application;

public interface IFloorPlanProjectStore
{
    Task<FloorPlanProject> ReadAsync(string path, CancellationToken cancellationToken);
    Task SaveAsync(FloorPlanProject project, string path, CancellationToken cancellationToken);
}
