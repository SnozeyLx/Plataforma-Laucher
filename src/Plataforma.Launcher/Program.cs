using Avalonia;
using Plataforma.Launcher.Services;
using System.Diagnostics;
using System.Threading;

namespace Plataforma.Launcher;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(
            initiallyOwned: false,
            LauncherInstanceCoordinator.MutexName,
            out var createdNew);
        if (!createdNew)
        {
            try
            {
                LauncherInstanceCoordinator.ForwardToRunningInstance(
                    LauncherProtocol.FindGameLaunchUri(args));
            }
            catch (IOException exception)
            {
                Trace.TraceError(
                    "Could not forward the launcher request to the running instance: {0}",
                    exception.GetType().Name);
            }
            catch (TimeoutException exception)
            {
                Trace.TraceError(
                    "Timed out forwarding the launcher request to the running instance: {0}",
                    exception.GetType().Name);
            }

            return;
        }

        mutex.WaitOne();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
