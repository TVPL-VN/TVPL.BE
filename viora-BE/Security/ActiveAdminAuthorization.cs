using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Viora.Application.Admin;

namespace viora_BE.Security;

public sealed class ActiveAdminRequirement : IAuthorizationRequirement;

public sealed class ActiveAdminAuthorization(IAdminWorkspaceService service) : AuthorizationHandler<ActiveAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveAdminRequirement requirement)
    {
        // Role claims can outlive a role change; the database checks the current
        // role, account status and deletion state for this authenticated account.
        if (context.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(context.User.FindFirstValue("sub"), out var accountId) &&
            await service.ActiveAdminAsync(accountId, CancellationToken.None) is not null)
            context.Succeed(requirement);
    }
}
