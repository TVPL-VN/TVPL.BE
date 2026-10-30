using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viora.Application.Accounts;
using Viora.Application.Users;
using Viora.Domain.Entities;
using Viora.Infrastructure.Persistence;

namespace Viora.Infrastructure.Security;

public sealed class AnktLoginService(AppDbContext db, IAnktPartnerClient partner, ITokenService tokens) : IAnktLoginService
{
    public async Task<LoginAccountResult> ExchangeAsync(string code, CancellationToken cancellationToken)
    {
        var verified = await partner.ExchangeAsync(code, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize creation for this exact composite identity across backend instances.
        // Hash collisions only serialize unrelated logins; uniqueness remains enforced by PostgreSQL.
        var lockKey = JsonSerializer.Serialize(new[] { verified.MiniAppId, verified.Environment, verified.User.Id });
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var identity = await db.AnktIdentities.Include(x => x.Account).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.MiniAppId == verified.MiniAppId &&
                x.Environment == verified.Environment && x.AnktUserId == verified.User.Id, cancellationToken);
        if (identity is null)
        {
            var account = new Account { IdentityProvider = "ankt" };
            account.User = new User { Account = account, DisplayName = string.Empty };
            account.User.Settings = new UserSettings { User = account.User };
            identity = new AnktIdentity
            {
                MiniAppId = verified.MiniAppId, Environment = verified.Environment,
                AnktUserId = verified.User.Id, Account = account
            };
            db.AnktIdentities.Add(identity);
        }
        var resolved = identity.Account;
        if (resolved.Status != AccountStatus.Active || resolved.DeletedAt is not null)
            throw new AnktAuthenticationException("ANKT_ACCOUNT_INACTIVE");
        var user = resolved.User ?? throw new AnktAuthenticationException("ANKT_ACCOUNT_PROFILE_MISSING");
        identity.ProfileJson = JsonSerializer.Serialize(verified.User, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        user.DisplayName = verified.User.DisplayName is { } name ? name[..Math.Min(100, name.Length)] : string.Empty;
        user.AvatarUrl = verified.User.AvatarUrl;
        user.CoverUrl = verified.User.CoverUrl;
        user.Gender = MapGender(verified.User.Gender);
        var binding = new AnktTokenBinding(Guid.NewGuid(), verified.ExpiresAt.UtcDateTime);
        var issued = tokens.CreateAnktTokens(resolved, binding);
        db.AnktGrants.Add(new AnktGrant
        {
            Id = binding.SessionId, Identity = identity,
            ProviderSessionId = verified.SessionId, ExpiresAt = binding.ExpiresAt
        });
        resolved.LastLoginAt = DateTime.UtcNow;
        db.RefreshTokens.Add(new RefreshToken
        {
            Account = resolved, SessionId = binding.SessionId, TokenHash = issued.RefreshTokenHash,
            ExpiresAt = issued.Tokens.RefreshTokenExpiresAt
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LoginAccountResult(LoginOutcome.Active, resolved.Status, null, issued.Tokens,
            new UserResponse(user.Id, resolved.Id, user.DisplayName, user.AvatarUrl, user.CoverUrl,
                user.Gender, resolved.Role, user.IsVerified, user.IdentityStatus, user.AccountStyle,
                ArticleCapabilities.CanPublish(user), ArticleCapabilities.CanManage(user)));
    }

    private static Gender MapGender(JsonElement? value)
    {
        // ANKT gender is retained losslessly in ProfileJson; map only known values.
        if (value is not { ValueKind: JsonValueKind.String } text) return Gender.Unknown;
        return text.GetString()?.ToLowerInvariant() switch
        {
            "male" => Gender.Male,
            "female" => Gender.Female,
            _ => Gender.Unknown
        };
    }
}
