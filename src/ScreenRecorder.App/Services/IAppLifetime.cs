using System.Windows;

namespace ScreenRecorder.App.Services;

public interface IAppLifetime
{
    void Shutdown();
}

public sealed class WpfAppLifetime : IAppLifetime
{
    public void Shutdown() => Application.Current.Shutdown();
}
