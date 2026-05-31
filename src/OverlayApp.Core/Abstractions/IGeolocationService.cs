using System.Threading;
using System.Threading.Tasks;

namespace OverlayApp.Core.Abstractions;

public sealed class GeolocationResult
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string? LocationName { get; init; }

    public bool HasError { get; init; }

    public string? ErrorMessage { get; init; }
}

public interface IGeolocationService
{
    Task<GeolocationResult> GetCurrentAsync(CancellationToken ct = default);
}
