using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Viora.Application.Accounts;
using Viora.Infrastructure.Persistence;

namespace Viora.Infrastructure.Security;

public sealed class AnktSessionValidator(AppDbContext db, IAnktPartnerClient partner,
    IOptions<AnktMiniAppOptions> options) : IAnktSessionValidator
{
    public async Task<AnktTokenBinding?> ValidateAsync(Guid sessionId, Guid accountId, CancellationToken cancellationToken)
    {
        var grant = await db.AnktGrants.AsNoTracking().Include(x => x.Identity)
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.Identity.AccountId == accountId, cancellationToken);
        if (grant is null || grant.Revoked || grant.ExpiresAt <= DateTime.UtcNow ||
            grant.Identity.MiniAppId != options.Value.ClientId || grant.Identity.Environment != options.Value.Environment)
            return null;
        if (!await db.RefreshTokens.AsNoTracking().AnyAsync(x => x.SessionId == sessionId &&
            x.AccountId == accountId && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow, cancellationToken))
            return null;
        try
        {
            var verified = await partner.SessionAsync(grant.ProviderSessionId, cancellationToken);
            // Expiry may be shortened by ANKT, but never extended beyond the stored grant.
            if (verified.SessionId != grant.ProviderSessionId || verified.MiniAppId != grant.Identity.MiniAppId ||
                verified.Environment != grant.Identity.Environment || verified.User.Id != grant.Identity.AnktUserId)
            {
                await RevokeAsync(sessionId, cancellationToken);
                return null;
            }
            var expiry = verified.ExpiresAt.UtcDateTime < grant.ExpiresAt ? verified.ExpiresAt.UtcDateTime : grant.ExpiresAt;
            return new AnktTokenBinding(sessionId, expiry);
        }
        catch (AnktAuthenticationException error)
        {
            // Outages reject this request but do not permanently revoke a potentially valid grant.
            if (!error.Unavailable) await RevokeAsync(sessionId, cancellationToken);
            return null;
        }
    }

    private Task<int> RevokeAsync(Guid sessionId, CancellationToken cancellationToken) =>
        db.AnktGrants.Where(x => x.Id == sessionId).ExecuteUpdateAsync(
            updates => updates.SetProperty(x => x.Revoked, true), cancellationToken);
}
