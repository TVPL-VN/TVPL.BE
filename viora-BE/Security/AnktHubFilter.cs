using Microsoft.AspNetCore.SignalR;
using Viora.Application.Accounts;

namespace viora_BE.Security;

// Long-lived connections must not continue invoking business methods after
// the upstream grant is revoked, even while their local JWT is unexpired.
public sealed class AnktHubFilter(IAnktSessionValidator sessions) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var claim = context.Context.User?.FindFirst("ankt_session")?.Value;
        if (claim is not null && (!Guid.TryParse(claim, out var sessionId) ||
            !Guid.TryParse(context.Context.User?.FindFirst("sub")?.Value, out var accountId) ||
            await sessions.ValidateAsync(sessionId, accountId, context.Context.ConnectionAborted) is null))
        {
            context.Context.Abort();
            throw new HubException("ANKT session expired. Reopen the Mini App.");
        }
        return await next(context);
    }
}
