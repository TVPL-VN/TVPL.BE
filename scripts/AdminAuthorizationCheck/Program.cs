using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Viora.Application.Admin;
using viora_BE.Security;

var accountId = Guid.NewGuid();
var scenarios = new[]
{
    (Name: "current admin claim", Authenticated: true, Subject: accountId.ToString(), Role: "2", ActiveAdmin: true, Allowed: true),
    (Name: "stale role claim with current admin account", Authenticated: true, Subject: accountId.ToString(), Role: "0", ActiveAdmin: true, Allowed: true),
    (Name: "missing role claim with current admin account", Authenticated: true, Subject: accountId.ToString(), Role: (string?)null, ActiveAdmin: true, Allowed: true),
    (Name: "revoked admin with stale admin claim", Authenticated: true, Subject: accountId.ToString(), Role: "2", ActiveAdmin: false, Allowed: false),
    (Name: "ordinary user", Authenticated: true, Subject: accountId.ToString(), Role: "0", ActiveAdmin: false, Allowed: false),
    (Name: "unauthenticated request", Authenticated: false, Subject: accountId.ToString(), Role: "2", ActiveAdmin: true, Allowed: false),
    (Name: "invalid account subject", Authenticated: true, Subject: "invalid", Role: "2", ActiveAdmin: true, Allowed: false),
};
var failures = 0;
foreach (var scenario in scenarios)
{
    var service = DispatchProxy.Create<IAdminWorkspaceService, AdminServiceProxy>();
    var proxy = (AdminServiceProxy)(object)service;
    proxy.ExpectedAccountId = accountId;
    proxy.ActiveAdmin = scenario.ActiveAdmin;
    var claims = new List<Claim> { new("sub", scenario.Subject) };
    if (scenario.Role is not null) claims.Add(new Claim("role", scenario.Role));
    var identity = new ClaimsIdentity(claims, scenario.Authenticated ? "Bearer" : null, "sub", "role");
    var requirement = new ActiveAdminRequirement();
    var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);
    await new ActiveAdminAuthorization(service).HandleAsync(context);
    var expectedLookup = scenario.Authenticated && Guid.TryParse(scenario.Subject, out _) ? 1 : 0;
    if (context.HasSucceeded != scenario.Allowed || proxy.Lookups != expectedLookup)
    {
        Console.Error.WriteLine($"FAIL: {scenario.Name}; allowed={context.HasSucceeded}, database checks={proxy.Lookups}");
        failures++;
    }
}
Console.WriteLine($"Authorization scenarios: {scenarios.Length}; failures: {failures}");
return failures == 0 ? 0 : 1;

public class AdminServiceProxy : DispatchProxy
{
    public Guid ExpectedAccountId { get; set; }
    public bool ActiveAdmin { get; set; }
    public int Lookups { get; private set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name != nameof(IAdminWorkspaceService.ActiveAdminAsync))
            throw new InvalidOperationException("Unexpected service call.");
        if ((Guid)args![0]! != ExpectedAccountId)
            throw new InvalidOperationException("Authorization checked the wrong account.");
        Lookups++;
        return Task.FromResult<Guid?>(ActiveAdmin ? Guid.NewGuid() : null);
    }
}
