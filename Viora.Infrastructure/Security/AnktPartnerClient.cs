using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Viora.Application.Accounts;

namespace Viora.Infrastructure.Security;

public sealed class AnktMiniAppOptions
{
    public const string SectionName = "AnktMiniApp";
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Environment { get; set; } = "Sandbox";
    public string ApiOrigin { get; set; } = "https://api.mxh.ankt.vn";
    public string HostOrigin { get; set; } = "https://mxh.ankt.vn";
    public string MiniAppOrigin { get; set; } = string.Empty;

    public bool IsValid() => !Enabled ||
        (System.Text.RegularExpressions.Regex.IsMatch(ClientId, "^app_[A-Za-z0-9_-]{43}$") &&
         !string.IsNullOrWhiteSpace(ClientSecret) && !ClientSecret.Contains(':') &&
         Environment is "Sandbox" or "Production" &&
         ApiOrigin == "https://api.mxh.ankt.vn" && HostOrigin == "https://mxh.ankt.vn" &&
         Uri.TryCreate(MiniAppOrigin, UriKind.Absolute, out var origin) &&
         origin.Scheme == "https" && origin.UserInfo.Length == 0 &&
         origin.GetLeftPart(UriPartial.Authority) == MiniAppOrigin);
}

public sealed class AnktPartnerClient(HttpClient http, IOptions<AnktMiniAppOptions> options) : IAnktPartnerClient
{
    public Task<AnktVerifiedGrant> ExchangeAsync(string code, CancellationToken cancellationToken) =>
        SendAsync("exchange", new { code }, cancellationToken);

    public Task<AnktVerifiedGrant> SessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        SendAsync("session", new { sessionId }, cancellationToken);

    private async Task<AnktVerifiedGrant> SendAsync(string path, object body, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled || !config.IsValid())
            throw new AnktAuthenticationException("ANKT_NOT_CONFIGURED", true);
        // A fresh server-owned request: never copy headers from HttpContext.
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{config.ApiOrigin}/api/miniapp/auth/partner/{path}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ClientId}:{config.ClientSecret}")));
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new AnktAuthenticationException("ANKT_GRANT_REJECTED",
                    (int)response.StatusCode >= 500 || (int)response.StatusCode == 429);
            var grant = await response.Content.ReadFromJsonAsync<AnktVerifiedGrant>(cancellationToken);
            if (grant is null || grant.MiniAppId != config.ClientId ||
                grant.Environment != config.Environment || grant.SessionId == Guid.Empty ||
                grant.User is null || string.IsNullOrWhiteSpace(grant.User.Id) || grant.User.Id.Length > 255 ||
                grant.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new AnktAuthenticationException("ANKT_INVALID_GRANT");
            return grant;
        }
        catch (HttpRequestException) { throw new AnktAuthenticationException("ANKT_UNAVAILABLE", true); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new AnktAuthenticationException("ANKT_TIMEOUT_NEW_CODE_REQUIRED", true); }
        catch (JsonException) { throw new AnktAuthenticationException("ANKT_INVALID_GRANT"); }
        catch (NotSupportedException) { throw new AnktAuthenticationException("ANKT_INVALID_GRANT"); }
    }
}
