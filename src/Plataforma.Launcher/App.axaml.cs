using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Plataforma.Launcher.Services;
using Plataforma.Launcher.ViewModels;
using Plataforma.Launcher.Views;

namespace Plataforma.Launcher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var apiBaseUrl = Environment.GetEnvironmentVariable("PLATAFORMA_API_BASE_URL")
                ?? "http://localhost:5000";
            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseAddress)
                || baseAddress.Scheme is not ("http" or "https"))
            {
                throw new InvalidOperationException(
                    "PLATAFORMA_API_BASE_URL must be an absolute HTTP or HTTPS URL.");
            }

            if (!string.IsNullOrEmpty(baseAddress.Query)
                || !string.IsNullOrEmpty(baseAddress.Fragment)
                || (baseAddress.Scheme == "http" && !baseAddress.IsLoopback))
            {
                throw new InvalidOperationException(
                    "PLATAFORMA_API_BASE_URL must not contain a query or fragment, and remote APIs must use HTTPS.");
            }

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(
                    baseAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
                        ? baseAddress.AbsoluteUri
                        : baseAddress.AbsoluteUri + "/")
            };
            var downloadHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromHours(2)
            };
            var viewModel = new MainViewModel(
                new PlatformApiClient(httpClient),
                new GameInstallationService(downloadHttpClient),
                LauncherProtocol.FindGameLaunchUri(desktop.Args));
            var mainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            desktop.MainWindow = mainWindow;

            var registrationWarning = LauncherProtocol.RegisterForCurrentUser();
            if (registrationWarning is not null)
            {
                viewModel.ErrorMessage = registrationWarning;
            }

            var instanceCoordinator = new LauncherInstanceCoordinator(launchUri =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
                    {
                        mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                    }

                    mainWindow.Activate();
                    if (launchUri is not null)
                    {
                        _ = viewModel.HandleLaunchUriAsync(launchUri, CancellationToken.None);
                    }
                }));

            desktop.Exit += (_, _) =>
            {
                instanceCoordinator.Dispose();
                httpClient.Dispose();
                downloadHttpClient.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}