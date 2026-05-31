namespace OverlayApp.Core.Abstractions;

public interface IOverlayController
{
    void SetClickThrough(bool enabled);

    void SetOpacity(double value);

    void SetTopMost(bool enabled);

    void Show();

    void Hide();

    bool IsVisible { get; }

    (double X, double Y) GetPosition();

    void SetPosition(double x, double y);

    (double Width, double Height) GetSize();

    void SetSize(double width, double height);
}
