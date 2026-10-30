using System.Text.Json;

namespace Viora.Application.Accounts;

public sealed record AnktUser(string Id, string? Username, string? DisplayName,
    string? AvatarUrl, string? CoverUrl, string? FullName, JsonElement? Gender);
public sealed record AnktVerifiedGrant(string MiniAppId, string Environment,
    Guid SessionId, DateTimeOffset ExpiresAt, AnktUser User);
public sealed record AnktTokenBinding(Guid SessionId, DateTime ExpiresAt);

public interface IAnktPartnerClient
{
    Task<AnktVerifiedGrant> ExchangeAsync(string code, CancellationToken cancellationToken);
    Task<AnktVerifiedGrant> SessionAsync(Guid sessionId, CancellationToken cancellationToken);
}

public interface IAnktSessionValidator
{
    Task<AnktTokenBinding?> ValidateAsync(Guid sessionId, Guid accountId, CancellationToken cancellationToken);
}

public interface IAnktLoginService
{
    Task<LoginAccountResult> ExchangeAsync(string code, CancellationToken cancellationToken);
}

// Deliberately contains no provider response body, code, token or credentials.
public sealed class AnktAuthenticationException(string code, bool unavailable = false) : Exception(code)
{
    public string Code { get; } = code;
    public bool Unavailable { get; } = unavailable;
}
