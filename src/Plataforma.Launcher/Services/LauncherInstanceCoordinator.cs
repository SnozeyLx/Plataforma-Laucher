using System.IO.Pipes;
using System.Text;
using System.Diagnostics;

namespace Plataforma.Launcher.Services;

/// <summary>
/// Forwards launcher activation and game requests to the running instance.
/// </summary>
public sealed class LauncherInstanceCoordinator : IDisposable
{
    private const string PipeName = "PlaylabLauncherActivation";
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _listenerTask;

    /// <summary>
    /// Gets the named mutex used to keep one launcher process per session.
    /// </summary>
    public static string MutexName { get; } = OperatingSystem.IsWindows()
        ? @"Local\PlaylabLauncher"
        : "PlaylabLauncher";

    /// <summary>
    /// Starts forwarding activation messages to the supplied handler.
    /// </summary>
    public LauncherInstanceCoordinator(Action<string?> activationHandler)
    {
        ArgumentNullException.ThrowIfNull(activationHandler);
        _listenerTask = ListenAsync(activationHandler);
    }

    /// <summary>
    /// Sends an activation request to the running launcher instance.
    /// </summary>
    public static void ForwardToRunningInstance(string? launchUri)
    {
        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        client.Connect(5000);
        using var writer = new StreamWriter(
            client,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: true)
        {
            AutoFlush = true
        };
        writer.WriteLine(launchUri ?? string.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _listenerTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _cancellation.Dispose();
        }
    }

    private async Task ListenAsync(Action<string?> activationHandler)
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                var pipeOptions = PipeOptions.Asynchronous;
                if (OperatingSystem.IsWindows())
                {
                    pipeOptions |= PipeOptions.CurrentUserOnly;
                }

                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    pipeOptions);
                await server.WaitForConnectionAsync(_cancellation.Token);
                using var reader = new StreamReader(
                    server,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);
                var request = await reader.ReadLineAsync(_cancellation.Token);
                activationHandler(string.IsNullOrWhiteSpace(request) ? null : request);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (IOException exception)
            {
                Trace.TraceError("Launcher activation pipe failed: {0}", exception.GetType().Name);
            }
        }
    }
}
