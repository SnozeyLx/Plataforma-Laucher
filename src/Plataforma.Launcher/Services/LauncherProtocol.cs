using System.Security;
using Microsoft.Win32;

namespace Plataforma.Launcher.Services;

/// <summary>
/// Parses and registers the launcher URI protocol on Windows.
/// </summary>
public static class LauncherProtocol
{
    private const string Scheme = "playlab";

    /// <summary>
    /// Finds a valid game launch URI among the process arguments.
    /// </summary>
    public static string? FindGameLaunchUri(IEnumerable<string>? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        return arguments.FirstOrDefault(argument => TryGetGameSlug(argument, out _));
    }

    /// <summary>
    /// Extracts a safe game slug from a launcher protocol URI.
    /// </summary>
    public static bool TryGetGameSlug(string? value, out string gameSlug)
    {
        gameSlug = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "game", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var candidate = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.Split('-').Any(segment =>
                segment.Length == 0 || segment.Any(character =>
                    !char.IsAsciiLetterOrDigit(character))))
        {
            return false;
        }

        gameSlug = candidate;
        return true;
    }

    /// <summary>
    /// Registers the custom URI protocol for the current Windows user.
    /// </summary>
    /// <returns>A safe warning if registration failed; otherwise, <see langword="null"/>.</returns>
    public static string? RegisterForCurrentUser()
    {
#if !RELEASE
        return null;
#else
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return "Não foi possível localizar o executável para registrar links do launcher.";
        }

        try
        {
            using var protocolKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\playlab");
            if (protocolKey is null)
            {
                return "Não foi possível registrar links do launcher para este usuário.";
            }

            protocolKey.SetValue(string.Empty, "URL:Playlab Game Launcher");
            protocolKey.SetValue("URL Protocol", string.Empty);
            using var commandKey = protocolKey.CreateSubKey(@"shell\open\command");
            if (commandKey is null)
            {
                return "Não foi possível registrar o comando de abertura do launcher.";
            }

            commandKey.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return "O Windows não permitiu registrar links do launcher para este usuário.";
        }
        catch (IOException)
        {
            return "Não foi possível salvar o registro de links do launcher no Windows.";
        }
        catch (SecurityException)
        {
            return "O Windows bloqueou o registro de links do launcher.";
        }
#endif
    }
}
