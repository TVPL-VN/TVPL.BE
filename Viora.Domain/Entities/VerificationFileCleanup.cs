namespace Viora.Domain.Entities;

// Durable cleanup after replacement/deletion; retries do not expose or lose private file references.
public sealed class VerificationFileCleanup : CreatedEntity
{
    public string FileKey { get; set; } = null!;
}
