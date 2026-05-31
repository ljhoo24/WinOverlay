using System;
using Avalonia.Threading;
using OverlayApp.Core.Abstractions;

namespace OverlayApp.Avalonia.Platform;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
