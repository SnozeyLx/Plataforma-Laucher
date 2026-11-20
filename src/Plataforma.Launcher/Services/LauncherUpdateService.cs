using System.Runtime.InteropServices;
using Velopack;

namespace Plataforma.Launcher.Services;

/// <summary>
/// Checks, downloads, and applies launcher updates from the platform API.
/// </summary>
public sealed class LauncherUpdateService
{
    private readonly UpdateManager updateManager;

    /// <summary>
    /// Initializes a launcher update service for the current operating system and architecture.
    /// </summary>
    /// <param name="apiBaseAddress">The base address of the platform API.</param>
    public LauncherUpdateService(Uri apiBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(apiBaseAddress);

        var channel = GetCurrentChannel()
            ?? throw new PlatformNotSupportedException(
                "Automatic launcher updates are not supported on this platform.");
        var updateFeedAddress = new Uri(
            apiBaseAddress,
            $"api/launcher/updates/{channel}");
        updateManager = new UpdateManager(updateFeedAddress.AbsoluteUri);
    }

    /// <summary>
    /// Gets whether the launcher is installed and can be updated by Velopack.
    /// </summary>
    public bool IsInstalled => updateManager.IsInstalled;

    /// <summary>
    /// Checks the current platform channel for a newer launcher release.
    /// </summary>
    /// <returns>The available update, or <see langword="null"/> when the launcher is current.</returns>
    public Task<UpdateInfo?> CheckForUpdatesAsync() =>
        updateManager.CheckForUpdatesAsync();

    /// <summary>
    /// Downloads and prepares an update, reporting progress from 0 to 100.
    /// </summary>
    /// <param name="update">The update returned by the latest check.</param>
    /// <param name="progress">Receives download progress percentages.</param>
    /// <param name="cancellationToken">A token that can cancel the download.</param>
    public Task DownloadUpdatesAsync(
        UpdateInfo update,
        IProgress<int> progress,
        CancellationToken cancellationToken) =>
        updateManager.DownloadUpdatesAsync(update, progress.Report, cancellationToken);

    /// <summary>
    /// Applies a prepared update and restarts the launcher.
    /// </summary>
    /// <param name="update">The update to apply.</param>
    /// <param name="restartArguments">Arguments to pass to the restarted launcher.</param>
    public void ApplyUpdatesAndRestart(UpdateInfo update, string[] restartArguments) =>
        updateManager.ApplyUpdatesAndRestart(update.TargetFullRelease, restartArguments);

    private static string? GetCurrentChannel()
    {
        var architecture = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows() && architecture == Architecture.X64)
        {
            return "win-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return architecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => null
            };
        }

        return OperatingSystem.IsLinux() && architecture == Architecture.X64
            ? "linux-x64"
            : null;
    }
}
