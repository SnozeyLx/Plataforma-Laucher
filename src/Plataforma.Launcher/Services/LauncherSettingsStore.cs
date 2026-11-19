using System.Text.Json;

namespace Plataforma.Launcher.Services;

public sealed class LauncherSettingsStore
{
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playlab",
        "launcher-settings.json");

    public string? LoadRememberedEmail()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<LauncherSettings>(
            File.ReadAllText(_settingsPath));
        return string.IsNullOrWhiteSpace(settings?.RememberedEmail)
            ? null
            : settings.RememberedEmail;
    }

    public void SaveRememberedEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(
            _settingsPath,
            JsonSerializer.Serialize(new LauncherSettings(email.Trim())));
    }

    public void ClearRememberedEmail()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    private sealed record LauncherSettings(string RememberedEmail);
}
