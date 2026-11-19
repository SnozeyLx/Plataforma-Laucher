using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Plataforma.Launcher.Services;

public sealed class PlatformApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient
        ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<LauncherSession> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(email, password),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, cancellationToken);
        }

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>(
            cancellationToken: cancellationToken)
            ?? throw new PlatformApiException(response.StatusCode, "A API retornou uma resposta de login vazia.");

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var userResponse = await _httpClient.SendAsync(userRequest, cancellationToken);

        if (!userResponse.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(userResponse, cancellationToken);
        }

        var user = await userResponse.Content.ReadFromJsonAsync<CurrentUserResponse>(
            cancellationToken: cancellationToken)
            ?? throw new PlatformApiException(
                userResponse.StatusCode,
                "A API retornou uma resposta de perfil vazia.");

        return new LauncherSession(user.Email, tokens.AccessToken, tokens.RefreshToken);
    }

    public async Task RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/forgot-password",
            new ForgotPasswordRequest(email),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, cancellationToken);
        }
    }

    public async Task LogoutAsync(LauncherSession session, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/logout",
            new RefreshTokenRequest(session.RefreshToken),
            cancellationToken);

        if (response.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.OK))
        {
            await ThrowApiExceptionAsync(response, cancellationToken);
        }
    }

    public async Task<PlayerProfileResponse> GetProfileAsync(
        LauncherSession session,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<PlayerProfileResponse>(
            cancellationToken: cancellationToken)
            ?? throw new PlatformApiException(response.StatusCode, "A API retornou um perfil vazio.");
    }

    public async Task<IReadOnlyList<GameLibraryItemResponse>> GetLibraryAsync(
        LauncherSession session,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/library");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<GameLibraryItemResponse[]>(
            cancellationToken: cancellationToken)
            ?? throw new PlatformApiException(response.StatusCode, "A API retornou uma biblioteca vazia.");
    }

    public async Task<GameBuildAccessResponse> GetBuildAccessAsync(
        LauncherSession session,
        string gameSlug,
        string platform,
        CancellationToken cancellationToken)
    {
        var path = $"api/games/{Uri.EscapeDataString(gameSlug)}/access?platform={Uri.EscapeDataString(platform)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<GameBuildAccessResponse>(
            cancellationToken: cancellationToken)
            ?? throw new PlatformApiException(response.StatusCode, "A API retornou um link vazio.");
    }

    public async Task<PlayerProfileResponse> UpdateProfileAsync(
        LauncherSession session,
        string playerName,
        int profileIconId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/profile")
        {
            Content = JsonContent.Create(
                new UpdatePlayerProfileRequest(playerName, profileIconId))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<PlayerProfileResponse>(
            cancellationToken: cancellationToken)
            ?? throw new PlatformApiException(response.StatusCode, "A API retornou um perfil vazio.");
    }

    private static async Task ThrowApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = TryReadProblemMessage(body)
            ?? $"The platform API returned HTTP {(int)response.StatusCode}.";

        throw new PlatformApiException(response.StatusCode, message);
    }

    private static string? TryReadProblemMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString();
            }

            if (document.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record ForgotPasswordRequest(string Email);

    private sealed record RefreshTokenRequest(string RefreshToken);

    private sealed record AuthTokensResponse(
        string TokenType,
        string AccessToken,
        DateTime AccessTokenExpiresAtUtc,
        string RefreshToken,
        DateTime RefreshTokenExpiresAtUtc);

    private sealed record CurrentUserResponse(Guid UserId, string Email);
}

public sealed record LauncherSession(string Email, string AccessToken, string RefreshToken);
public sealed record PlayerProfileResponse(Guid UserId, string? PlayerName, int ProfileIconId);
public sealed record UpdatePlayerProfileRequest(string PlayerName, int ProfileIconId);
public sealed record GameLibraryItemResponse(
    string Slug,
    string Title,
    string? Description,
    bool IsOwned,
    DateTimeOffset? GrantedAtUtc,
    bool WindowsBuildAvailable,
    bool WebGlBuildAvailable,
    bool LauncherDownloadAvailable);
public sealed record GameBuildAccessResponse(
    string GameSlug,
    string Platform,
    string Url,
    DateTimeOffset ExpiresAtUtc);

public sealed class PlatformApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
