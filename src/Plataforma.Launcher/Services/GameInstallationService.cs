using System.IO.Compression;

namespace Plataforma.Launcher.Services;

/// <summary>
/// Downloads and safely installs Windows game archives for the current user.
/// </summary>
public sealed class GameInstallationService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient
        ?? throw new ArgumentNullException(nameof(httpClient));

    /// <summary>
    /// Downloads a ZIP archive, extracts it to the user's game directory, and returns launchable executables.
    /// </summary>
    public async Task<IReadOnlyList<string>> DownloadAndInstallAsync(
        Uri archiveUri,
        string gameSlug,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(archiveUri);
        if (archiveUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(archiveUri.UserInfo))
        {
            throw new ArgumentException(
                "The build URL must use HTTPS and cannot contain user information.",
                nameof(archiveUri));
        }

        var gameRoot = GetGameRoot(gameSlug);
        Directory.CreateDirectory(gameRoot);

        var workingDirectory = Path.Combine(gameRoot, $".install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var archivePath = Path.Combine(workingDirectory, "build.zip");
            using (var response = await _httpClient.GetAsync(
                       archiveUri,
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken))
            {
                var finalUri = response.RequestMessage?.RequestUri;
                if (finalUri is null
                    || finalUri.Scheme != Uri.UriSchemeHttps
                    || !string.IsNullOrEmpty(finalUri.UserInfo))
                {
                    throw new HttpRequestException("The build download redirected to an invalid URL.");
                }

                response.EnsureSuccessStatusCode();
                await using var archiveStream = new FileStream(
                    archivePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await contentStream.CopyToAsync(archiveStream, cancellationToken);
            }

            var extractedDirectory = Path.Combine(workingDirectory, "extracted");
            Directory.CreateDirectory(extractedDirectory);
            ZipFile.ExtractToDirectory(archivePath, extractedDirectory);

            var executablePaths = Directory
                .EnumerateFiles(extractedDirectory, "*.exe", SearchOption.AllDirectories)
                .Where(path => !IsHelperExecutable(Path.GetFileName(path)))
                .Select(path => Path.GetRelativePath(extractedDirectory, path))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (executablePaths.Length == 0)
            {
                throw new InvalidDataException("The downloaded archive contains no game executable.");
            }

            var installDirectory = Path.Combine(gameRoot, "current");
            var backupDirectory = Path.Combine(gameRoot, $".previous-{Guid.NewGuid():N}");
            var hasPreviousInstall = Directory.Exists(installDirectory);
            if (hasPreviousInstall)
            {
                Directory.Move(installDirectory, backupDirectory);
            }

            try
            {
                Directory.Move(extractedDirectory, installDirectory);
            }
            catch
            {
                if (hasPreviousInstall && !Directory.Exists(installDirectory))
                {
                    Directory.Move(backupDirectory, installDirectory);
                }

                throw;
            }

            if (hasPreviousInstall)
            {
                Directory.Delete(backupDirectory, recursive: true);
            }

            return executablePaths
                .Select(relativePath => Path.Combine(installDirectory, relativePath))
                .ToArray();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Finds installed game executables for the specified game.
    /// </summary>
    public IReadOnlyList<string> FindInstalledExecutables(string gameSlug)
    {
        var installDirectory = Path.Combine(GetGameRoot(gameSlug), "current");
        if (!Directory.Exists(installDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(installDirectory, "*.exe", SearchOption.AllDirectories)
            .Where(path => !IsHelperExecutable(Path.GetFileName(path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetGameRoot(string gameSlug)
    {
        if (string.IsNullOrWhiteSpace(gameSlug)
            || gameSlug.Split('-').Any(segment =>
                segment.Length == 0 || segment.Any(character =>
                    !char.IsAsciiLetterOrDigit(character))))
        {
            throw new ArgumentException(
                "The game slug is not a valid installation directory name.",
                nameof(gameSlug));
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Playlab",
            "Games",
            gameSlug);
    }

    private static bool IsHelperExecutable(string fileName) =>
        fileName.StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "crashpad_handler.exe", StringComparison.OrdinalIgnoreCase);
}
