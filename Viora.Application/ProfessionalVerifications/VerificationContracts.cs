using Viora.Domain.Entities;

namespace Viora.Application.ProfessionalVerifications;

public sealed record VerificationDocumentResponse(Guid Id, VerificationDocumentType DocumentType,
    string OriginalFileName, string ContentType, long FileSize, DateTime CreatedAt);
public sealed record VerificationResponse(Guid Id, ProfessionalVerificationType Type, VerificationStatus Status,
    LawyerVerificationData VerificationData, int CompletedStep, int Revision, DateTime? SubmittedAt,
    DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<VerificationDocumentResponse> Documents);
public sealed record SaveVerificationRequest(LawyerVerificationData VerificationData, int CompletedStep, int Revision);
public sealed record SubmitVerificationRequest(bool Confirmed, int Revision);
public sealed record VerificationFile(Stream Content, string FileName, string ContentType, long Length);
public sealed record PrivateDocumentContent(byte[] Content, string ContentType, string FileName);

public sealed class VerificationException(int status, string code, string message,
    IDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public IDictionary<string, string[]>? Errors { get; } = errors;
}

public interface IProfessionalVerificationService
{
    Task<VerificationResponse> StartAsync(Guid accountId, CancellationToken ct);
    Task<VerificationResponse?> GetMineAsync(Guid accountId, CancellationToken ct);
    Task<VerificationResponse> SaveAsync(Guid accountId, Guid id, SaveVerificationRequest request, CancellationToken ct);
    Task<VerificationResponse> UploadAsync(Guid accountId, Guid id, VerificationDocumentType type, VerificationFile file, CancellationToken ct);
    Task<VerificationResponse> DeleteAsync(Guid accountId, Guid id, Guid documentId, CancellationToken ct);
    Task<PrivateDocumentContent> ReadAsync(Guid accountId, Guid id, Guid documentId, CancellationToken ct);
    Task<VerificationResponse> SubmitAsync(Guid accountId, Guid id, SubmitVerificationRequest request, CancellationToken ct);
}

// All keys and signed provider URLs stay inside Infrastructure. Nothing here is a public delivery URL.
public interface IPrivateFileStorage
{
    Task<string> UploadAsync(Guid accountId, Guid verificationId, VerificationFile file, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
