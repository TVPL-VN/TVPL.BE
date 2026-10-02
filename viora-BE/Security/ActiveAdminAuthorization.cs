using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Viora.Application.Admin;

namespace viora_BE.Security;

public sealed class ActiveAdminRequirement : IAuthorizationRequirement;

public sealed class ActiveAdminAuthorization(IAdminWorkspaceService service) : AuthorizationHandler<ActiveAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveAdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("2") &&
            Guid.TryParse(context.User.FindFirstValue("sub"), out var accountId) &&
            await service.ActiveAdminAsync(accountId, CancellationToken.None) is not null)
            context.Succeed(requirement);
    }
}
