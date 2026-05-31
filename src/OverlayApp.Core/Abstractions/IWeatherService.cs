using System.Threading;
using System.Threading.Tasks;
using OverlayApp.Core.Models;

namespace OverlayApp.Core.Abstractions;

public interface IWeatherService
{
    Task<WeatherInfo> GetByCoordinatesAsync(double latitude, double longitude, CancellationToken ct = default);

    Task<WeatherInfo> GetByCityAsync(string cityName, CancellationToken ct = default);
}
