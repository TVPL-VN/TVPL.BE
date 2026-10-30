namespace Viora.Domain.Entities;

public sealed class AnktIdentity : AuditableEntity
{
    public Guid AccountId { get; set; }
    public string MiniAppId { get; set; } = null!;
    public string Environment { get; set; } = null!;
    public string AnktUserId { get; set; } = null!;
    public string ProfileJson { get; set; } = "{}";
    public Account Account { get; set; } = null!;
}

// Id is a Viora session ID; the upstream grant ID never comes from the browser.
public sealed class AnktGrant : AuditableEntity
{
    public Guid IdentityId { get; set; }
    public Guid ProviderSessionId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public AnktIdentity Identity { get; set; } = null!;
}
