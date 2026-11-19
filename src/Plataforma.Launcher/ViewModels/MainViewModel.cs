using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plataforma.Launcher.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Net;

namespace Plataforma.Launcher.ViewModels;

public partial class MainViewModel(
    PlatformApiClient apiClient,
    GameInstallationService gameInstallationService,
    string? launchUri = null,
    LauncherSettingsStore? settingsStore = null) : ViewModelBase
{
    private const string ServerManagedGameSlug = "dash-and-crash";
    private readonly LauncherSettingsStore _settingsStore = settingsStore ?? new();
    private bool _isLoadingSettings;
    private string? pendingGameSlug = LauncherProtocol.TryGetGameSlug(launchUri, out var gameSlug)
        ? gameSlug
        : null;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RememberEmail { get; set; }

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SignedInEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAuthenticated { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ObservableCollection<LibraryGameViewModel> PurchasedGames { get; } = [];

    [ObservableProperty]
    public partial LibraryGameViewModel? SelectedGame { get; set; }

    [ObservableProperty]
    public partial string AccountInitial { get; set; } = "P";

    [ObservableProperty]
    public partial string PlayerName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ProfileIconId { get; set; } = 1;

    [ObservableProperty]
    public partial string ProfileIconGlyph { get; set; } = "◆";

    public bool IsProfileIcon1 => ProfileIconId == 1;
    public bool IsProfileIcon2 => ProfileIconId == 2;
    public bool IsProfileIcon3 => ProfileIconId == 3;
    public bool IsProfileIcon4 => ProfileIconId == 4;
    public bool IsProfileIcon5 => ProfileIconId == 5;
    public bool IsProfileIcon6 => ProfileIconId == 6;
    public bool IsProfileIcon7 => ProfileIconId == 7;
    public bool IsProfileIcon8 => ProfileIconId == 8;

    [ObservableProperty]
    public partial bool IsProfileModalOpen { get; set; }

    [ObservableProperty]
    public partial string CurrentSection { get; set; } = "Library";

    public bool IsLibrarySection => CurrentSection == "Library";

    public bool IsStoreSection => CurrentSection == "Store";

    public ObservableCollection<StoreGameViewModel> StoreGames { get; } =
    [
        CreateStoreTemplate("Dash & Crash", "dash-and-crash", "Ação / Corrida",
            "O modelo principal da plataforma. Corrida rápida, fases dinâmicas e ação cooperativa.",
            "Acesso de teste", "D", true,
            ("Pacote Neon Racer", "Skin premium com efeitos luminosos", "R$ 12,90"),
            ("Passe da Primeira Temporada", "Itens cosméticos e recompensas da temporada", "R$ 24,90")),
        CreateStoreTemplate("Void Horizon", "void-horizon", "Sci-Fi / Sobrevivência",
            "Explore setores abandonados e construa seu esquadrão em uma fronteira hostil.",
            "R$ 59,90", "V", false,
            ("Traje Quantum", "Conjunto cosmético legendário", "R$ 18,90"),
            ("Mapa Élitro Azul", "Expansão visual exclusiva", "R$ 9,90")),
        CreateStoreTemplate("Arena Myth", "arena-myth", "Estratégia / Fantasia",
            "Monte equipes lendárias e dispute arenas inspiradas em mitologias clássicas.",
            "R$ 49,90", "-20%", false,
            ("Pacote Guardiões", "Quatro personagens cosméticos", "R$ 21,90"),
            ("Temporada dos Deuses", "Passe de temporada completo", "R$ 29,90"))
    ];

    private readonly Dictionary<string, ObservableCollection<LibraryGameViewModel>> libraryByAccount = [];

    private LauncherSession? session;

    [RelayCommand]
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        IsBusy = true;

        try
        {
            session = await apiClient.LoginAsync(Email.Trim(), Password, cancellationToken);
            SignedInEmail = session.Email;
            Password = string.Empty;
            SaveRememberedEmail(session.Email);
            IsAuthenticated = true;
            AccountInitial = GetAccountInitial(session.Email);
            await LoadProfileAsync(cancellationToken);
            RestoreLibrary(session.Email);
            await LoadLibraryAsync(cancellationToken);
            CurrentSection = "Library";
            if (pendingGameSlug is not null)
            {
                await ProcessPendingGameLaunchAsync(cancellationToken);
            }
            else
            {
                SelectedGame = PurchasedGames.FirstOrDefault();
            }
        }
        catch (PlatformApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Não foi possível conectar à plataforma. Verifique a API e tente novamente.";
        }
        catch (JsonException)
        {
            ErrorMessage = "A API retornou uma resposta inválida. Tente novamente mais tarde.";
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "A conexão com a plataforma expirou. Tente novamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ForgotPasswordAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Informe seu e-mail para solicitar a recuperação da senha.";
            return;
        }

        IsBusy = true;
        try
        {
            await apiClient.RequestPasswordResetAsync(Email.Trim(), cancellationToken);
            StatusMessage =
                "Se houver uma conta com esse e-mail, enviaremos um link para redefinir a senha.";
        }
        catch (PlatformApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Não foi possível conectar à plataforma. Tente novamente.";
        }
        catch (JsonException)
        {
            ErrorMessage = "A API retornou uma resposta inválida. Tente novamente mais tarde.";
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "A conexão com a plataforma expirou. Tente novamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            if (session is not null)
            {
                await apiClient.LogoutAsync(session, cancellationToken);
            }
        }
        catch (PlatformApiException exception)
        {
            ErrorMessage = $"A sessão foi encerrada neste launcher, mas a API não confirmou o logout: {exception.Message}";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "A sessão foi encerrada neste launcher, mas não foi possível confirmar o logout na API.";
        }
        finally
        {
            session = null;
            SignedInEmail = string.Empty;
            IsAuthenticated = false;
            IsBusy = false;
        }
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnRememberEmailChanged(bool value)
    {
        if (_isLoadingSettings || value)
        {
            return;
        }

        try
        {
            _settingsStore.ClearRememberedEmail();
        }
        catch (IOException)
        {
            ErrorMessage = "Não foi possível apagar o e-mail salvo neste dispositivo.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Não foi possível apagar o e-mail salvo neste dispositivo.";
        }
    }

    public void LoadRememberedEmail()
    {
        _isLoadingSettings = true;
        try
        {
            Email = _settingsStore.LoadRememberedEmail() ?? string.Empty;
            RememberEmail = !string.IsNullOrWhiteSpace(Email);
        }
        catch (IOException)
        {
            ErrorMessage = "Não foi possível carregar as preferências do launcher.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Não foi possível carregar as preferências do launcher.";
        }
        catch (JsonException)
        {
            ErrorMessage = "As preferências salvas do launcher estão inválidas.";
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void SaveRememberedEmail(string email)
    {
        try
        {
            if (RememberEmail)
            {
                _settingsStore.SaveRememberedEmail(email);
            }
            else
            {
                _settingsStore.ClearRememberedEmail();
            }
        }
        catch (IOException)
        {
            StatusMessage = "Login realizado, mas não foi possível salvar o e-mail neste dispositivo.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Login realizado, mas não foi possível salvar o e-mail neste dispositivo.";
        }
    }

    partial void OnCurrentSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsLibrarySection));
        OnPropertyChanged(nameof(IsStoreSection));
    }

    /// <summary>
    /// Opens the requested owned game, downloading its Windows build when it is not installed.
    /// </summary>
    public async Task HandleLaunchUriAsync(
        string launchUri,
        CancellationToken cancellationToken)
    {
        if (!LauncherProtocol.TryGetGameSlug(launchUri, out var gameSlug))
        {
            ErrorMessage = "O link recebido pelo launcher é inválido.";
            return;
        }

        pendingGameSlug = gameSlug;
        CurrentSection = "Library";
        if (session is null)
        {
            ErrorMessage = "Entre na sua conta para abrir o jogo solicitado.";
            return;
        }

        await ProcessPendingGameLaunchAsync(cancellationToken);
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        CurrentSection = "Library";
        SelectedGame ??= PurchasedGames.FirstOrDefault();
    }

    [RelayCommand]
    private void ShowStore()
    {
        CurrentSection = "Store";
    }

    [RelayCommand]
    private void OpenProfile()
    {
        IsProfileModalOpen = true;
    }

    [RelayCommand]
    private void CloseProfile()
    {
        IsProfileModalOpen = false;
    }

    [RelayCommand]
    private void SelectProfileIcon(string iconId)
    {
        if (int.TryParse(iconId, out var parsedIconId))
        {
            ProfileIconId = parsedIconId;
        }
    }

    partial void OnProfileIconIdChanged(int value)
    {
        ProfileIconGlyph = value switch
        {
            2 => "●",
            3 => "▲",
            4 => "■",
            5 => "★",
            6 => "⬟",
            7 => "♥",
            8 => "☾",
            _ => "◆"
        };

        OnPropertyChanged(nameof(IsProfileIcon1));
        OnPropertyChanged(nameof(IsProfileIcon2));
        OnPropertyChanged(nameof(IsProfileIcon3));
        OnPropertyChanged(nameof(IsProfileIcon4));
        OnPropertyChanged(nameof(IsProfileIcon5));
        OnPropertyChanged(nameof(IsProfileIcon6));
        OnPropertyChanged(nameof(IsProfileIcon7));
        OnPropertyChanged(nameof(IsProfileIcon8));
    }

    [RelayCommand]
    private async Task SaveProfileAsync(CancellationToken cancellationToken)
    {
        if (session is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var profile = await apiClient.UpdateProfileAsync(
                session,
                PlayerName.Trim(),
                ProfileIconId,
                cancellationToken);
            PlayerName = profile.PlayerName ?? PlayerName;
            ProfileIconId = profile.ProfileIconId;
            AccountInitial = GetAccountInitial(profile.PlayerName ?? session.Email);
            IsProfileModalOpen = false;
        }
        catch (PlatformApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Não foi possível salvar o perfil.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BuyGame(StoreGameViewModel? game)
    {
        if (game is null || game.IsPurchased)
        {
            return;
        }

        if (game.Slug == ServerManagedGameSlug)
        {
            ErrorMessage = "Dash & Crash só é adicionado pela licença sandbox confirmada na API.";
            return;
        }

        if (session is not null)
        {
            var libraryGame = new LibraryGameViewModel
            {
                Title = game.Title,
                Slug = game.Slug,
                Genre = game.Genre,
                Description = game.Description,
                Status = "Disponível",
                LastPlayed = "Nunca iniciado",
                PlayTime = "0 horas",
                InstalledVersion = "Não instalado"
            };
            PurchasedGames.Add(libraryGame);
            game.IsPurchased = true;
            libraryByAccount[NormalizeAccountKey(session.Email)] = PurchasedGames;
            SelectedGame = libraryGame;
            CurrentSection = "Library";
        }
    }

    private void RestoreLibrary(string email)
    {
        var key = NormalizeAccountKey(email);
        if (libraryByAccount.TryGetValue(key, out var accountLibrary))
        {
            PurchasedGames.Clear();
            foreach (var game in accountLibrary.Where(
                         game => !string.Equals(
                             game.Slug,
                             ServerManagedGameSlug,
                             StringComparison.OrdinalIgnoreCase)))
            {
                PurchasedGames.Add(game);
            }
        }
        else
        {
            PurchasedGames.Clear();
            libraryByAccount[key] = PurchasedGames;
        }

        foreach (var storeGame in StoreGames)
        {
            storeGame.IsPurchased = PurchasedGames.Any(
                game => string.Equals(game.Slug, storeGame.Slug, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task LoadLibraryAsync(CancellationToken cancellationToken)
    {
        if (session is null)
        {
            return;
        }

        var serverLibrary = await apiClient.GetLibraryAsync(session, cancellationToken);
        foreach (var serverGame in serverLibrary.Where(game => game.IsOwned))
        {
            var libraryGame = PurchasedGames.FirstOrDefault(
                game => string.Equals(game.Slug, serverGame.Slug, StringComparison.OrdinalIgnoreCase));
            if (libraryGame is null)
            {
                libraryGame = new LibraryGameViewModel();
                PurchasedGames.Add(libraryGame);
            }

            libraryGame.Title = serverGame.Title;
            libraryGame.Slug = serverGame.Slug;
            libraryGame.Description = serverGame.Description ?? string.Empty;
            libraryGame.Genre = serverGame.Slug == ServerManagedGameSlug
                ? "Ação / Corrida"
                : "Jogo digital";
            libraryGame.Status = "Licença confirmada pela API";
            libraryGame.LastPlayed = "Nunca iniciado";
            libraryGame.PlayTime = "0 horas";
            libraryGame.InstalledVersion = "Build não configurado";
            libraryGame.IsWindowsBuildAvailable = serverGame.WindowsBuildAvailable;
            libraryGame.IsWebGlBuildAvailable = serverGame.WebGlBuildAvailable;
            var installedExecutables = gameInstallationService.FindInstalledExecutables(serverGame.Slug);
            libraryGame.ExecutableCandidates.Clear();
            foreach (var executablePath in installedExecutables)
            {
                libraryGame.ExecutableCandidates.Add(executablePath);
            }

            libraryGame.IsInstalled = installedExecutables.Count > 0;
            libraryGame.IsExecutableSelectionRequired = installedExecutables.Count > 1;
            libraryGame.SelectedExecutablePath = installedExecutables.Count == 1
                ? installedExecutables[0]
                : null;
            if (installedExecutables.Count > 0)
            {
                libraryGame.InstalledVersion = "Instalado localmente";
            }
        }

        foreach (var storeGame in StoreGames)
        {
            storeGame.IsPurchased = PurchasedGames.Any(
                game => string.Equals(game.Slug, storeGame.Slug, StringComparison.OrdinalIgnoreCase));
        }

        libraryByAccount[NormalizeAccountKey(session.Email)] = PurchasedGames;
    }

    [RelayCommand]
    private async Task DownloadWindowsBuildAsync(CancellationToken cancellationToken)
    {
        if (session is null || SelectedGame is null)
        {
            return;
        }

        var previousStatus = SelectedGame.Status;
        SelectedGame.Status = "Baixando build do Windows...";
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var access = await apiClient.GetBuildAccessAsync(
                session,
                SelectedGame.Slug,
                "windows",
                cancellationToken);
            if (!Uri.TryCreate(access.Url, UriKind.Absolute, out var buildUri)
                || buildUri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(buildUri.UserInfo))
            {
                throw new InvalidOperationException("The API returned an invalid build URL.");
            }

            var executablePaths = await gameInstallationService.DownloadAndInstallAsync(
                buildUri,
                SelectedGame.Slug,
                cancellationToken);
            SelectedGame.ExecutableCandidates.Clear();
            foreach (var executablePath in executablePaths)
            {
                SelectedGame.ExecutableCandidates.Add(executablePath);
            }

            SelectedGame.IsInstalled = true;
            SelectedGame.IsExecutableSelectionRequired = executablePaths.Count > 1;
            SelectedGame.SelectedExecutablePath = executablePaths.Count == 1
                ? executablePaths[0]
                : null;
            SelectedGame.InstalledVersion = "Instalado localmente";
            SelectedGame.Status = "Pronto para jogar";
            if (executablePaths.Count > 1)
            {
                ErrorMessage = "Instalação concluída. Selecione o executável principal antes de jogar.";
            }
        }
        catch (PlatformApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is { } statusCode)
        {
            ErrorMessage = statusCode switch
            {
                HttpStatusCode.NotFound =>
                    "O arquivo do jogo não foi encontrado na CDN (HTTP 404). Confira o caminho do build configurado na API.",
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                    $"A CDN recusou o download (HTTP {(int)statusCode}). Confira a Token Authentication e o caminho do arquivo.",
                _ => $"O servidor de arquivos respondeu HTTP {(int)statusCode}."
            };
        }
        catch (HttpRequestException exception)
        {
            var cause = exception.InnerException?.GetType().Name;
            ErrorMessage = string.IsNullOrWhiteSpace(cause)
                ? "Falha de rede ao baixar o build. Verifique DNS, TLS e a conexão com a CDN."
                : $"Falha de rede ao baixar o build ({cause}). Verifique DNS, TLS e a conexão com a CDN.";
        }
        catch (InvalidDataException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (IOException)
        {
            ErrorMessage = "Não foi possível gravar ou instalar o build neste dispositivo.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "O launcher não tem permissão para instalar o build neste dispositivo.";
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = "A API retornou um link de build inválido.";
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "O download do build expirou. Tente novamente.";
        }
        finally
        {
            if (SelectedGame is not null
                && SelectedGame.Status == "Baixando build do Windows...")
            {
                SelectedGame.Status = SelectedGame.IsInstalled
                    ? "Pronto para jogar"
                    : previousStatus;
            }

            IsBusy = false;
        }
    }

    private async Task ProcessPendingGameLaunchAsync(CancellationToken cancellationToken)
    {
        if (session is null || pendingGameSlug is null)
        {
            return;
        }

        var requestedSlug = pendingGameSlug;
        pendingGameSlug = null;
        var requestedGame = PurchasedGames.FirstOrDefault(
            game => string.Equals(game.Slug, requestedSlug, StringComparison.OrdinalIgnoreCase));
        if (requestedGame is null)
        {
            ErrorMessage = "Este jogo não está na sua biblioteca.";
            return;
        }

        SelectedGame = requestedGame;
        CurrentSection = "Library";
        if (requestedGame.IsInstalled)
        {
            PlayInstalledBuild();
            return;
        }

        if (!requestedGame.IsWindowsBuildAvailable)
        {
            ErrorMessage = "A build para Windows ainda não está publicada. Use o WebGL quando estiver disponível.";
            return;
        }

        await DownloadWindowsBuildAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task PlayWebGlBuildAsync(CancellationToken cancellationToken)
    {
        await OpenGameBuildAsync("webgl", cancellationToken);
    }

    private async Task OpenGameBuildAsync(string platform, CancellationToken cancellationToken)
    {
        if (session is null || SelectedGame is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var access = await apiClient.GetBuildAccessAsync(
                session,
                SelectedGame.Slug,
                platform,
                cancellationToken);
            if (!Uri.TryCreate(access.Url, UriKind.Absolute, out var buildUri)
                || buildUri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(buildUri.UserInfo))
            {
                throw new InvalidOperationException("The API returned an invalid build URL.");
            }

            using var openedProcess = Process.Start(
                new ProcessStartInfo(buildUri.AbsoluteUri) { UseShellExecute = true })
                ?? throw new InvalidOperationException("The build URL could not be opened.");
        }
        catch (PlatformApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Não foi possível solicitar acesso ao build.";
        }
        catch (Win32Exception)
        {
            ErrorMessage = "Não foi possível abrir o link seguro no navegador.";
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = "Não foi possível abrir o link seguro no navegador.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void PlayInstalledBuild()
    {
        if (SelectedGame?.SelectedExecutablePath is not { } executablePath
            || !SelectedGame.ExecutableCandidates.Contains(executablePath)
            || !File.Exists(executablePath))
        {
            ErrorMessage = "Selecione um executável instalado válido antes de jogar.";
            return;
        }

        ErrorMessage = string.Empty;
        try
        {
            using var openedProcess = Process.Start(
                new ProcessStartInfo(executablePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(executablePath)!
                })
                ?? throw new InvalidOperationException("The game executable could not be started.");
            SelectedGame.LastPlayed = $"Iniciado em {DateTime.Now:g}";
        }
        catch (Win32Exception)
        {
            ErrorMessage = "O Windows não conseguiu iniciar o jogo.";
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = "Não foi possível iniciar o executável instalado.";
        }
    }

    private static string NormalizeAccountKey(string email) =>
        email.Trim().ToUpperInvariant();

    private async Task LoadProfileAsync(CancellationToken cancellationToken)
    {
        if (session is null)
        {
            return;
        }

        try
        {
            var profile = await apiClient.GetProfileAsync(session, cancellationToken);
            PlayerName = profile.PlayerName ?? session.Email;
            ProfileIconId = profile.ProfileIconId;
            AccountInitial = GetAccountInitial(profile.PlayerName ?? session.Email);
        }
        catch (PlatformApiException)
        {
            PlayerName = session.Email;
        }
    }

    private static StoreGameViewModel CreateStoreTemplate(
        string title,
        string slug,
        string genre,
        string description,
        string price,
        string coverInitial,
        bool isSandboxManaged,
        params (string Name, string Description, string Price)[] items)
    {
        var game = new StoreGameViewModel
        {
            Title = title,
            Slug = slug,
            Genre = genre,
            Description = description,
            Price = price,
            CoverInitial = coverInitial,
            IsSandboxManaged = isSandboxManaged
        };

        foreach (var item in items)
        {
            game.Items.Add(new StoreItemViewModel
            {
                Name = item.Name,
                Description = item.Description,
                Price = item.Price
            });
        }

        return game;
    }

    private static string GetAccountInitial(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? "P"
            : email.Trim()[0].ToString().ToUpperInvariant();
    }
}
