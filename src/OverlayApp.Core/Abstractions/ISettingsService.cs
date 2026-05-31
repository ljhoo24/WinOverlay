using OverlayApp.Core.Models;

namespace OverlayApp.Core.Abstractions;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
