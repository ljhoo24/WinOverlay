using System;

namespace OverlayApp.Core.Abstractions;

public interface IUiDispatcher
{
    void Post(Action action);
}
