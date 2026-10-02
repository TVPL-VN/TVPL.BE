using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viora.Application.ProfessionalVerifications;
using Viora.Domain.Entities;

namespace Viora.Infrastructure.Persistence.Repositories;

public sealed class ProfessionalVerificationService(AppDbContext db, IPrivateFileStorage storage,
    ILogger<ProfessionalVerificationService> logger) : IProfessionalVerificationService
{
    public async Task<VerificationResponse?> GetMineAsync(Guid accountId, CancellationToken ct)
    {
        await RequireAccount(accountId, ct);
        var entity = await db.ProfessionalVerifications.AsNoTracking().Include(x => x.Documents)
            .Where(x => x.AccountId == accountId && x.Type == ProfessionalVerificationType.Lawyer)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        return entity is null ? null : Response(entity);
    }

    public async Task<VerificationResponse> StartAsync(Guid accountId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAccount(accountId, ct);
        var entity = await db.ProfessionalVerifications.Include(x => x.Documents)
            .Where(x => x.AccountId == accountId && x.Type == ProfessionalVerificationType.Lawyer)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (entity is null)
        {
            entity = new ProfessionalVerification { AccountId = accountId };
            db.ProfessionalVerifications.Add(entity);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Response(entity);
    }

    public async Task<VerificationResponse> SaveAsync(Guid accountId, Guid id, SaveVerificationRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await LockedDraft(accountId, id, ct);
        RequireRevision(entity, request.Revision);
        entity.VerificationData = LawyerVerificationValidation.Validate(request.VerificationData, request.CompletedStep);
        if (request.CompletedStep >= 3) LawyerVerificationValidation.RequireDocuments(entity.Documents.Select(x => x.DocumentType));
        entity.CompletedStep = request.CompletedStep;
        entity.Revision++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(entity);
    }

    public async Task<VerificationResponse> UploadAsync(Guid accountId, Guid id, VerificationDocumentType type, VerificationFile file, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await LockedDraft(accountId, id, ct);
        await LawyerVerificationValidation.ValidateFileAsync(file, type, ct);
        var old = entity.Documents.SingleOrDefault(x => x.DocumentType == type);
        var key = await storage.UploadAsync(accountId, id, file, ct);
        var committed = false;
        try
        {
            if (old is not null)
            {
                QueueCleanup(old.FileKey);
                old.FileKey = key;
                old.OriginalFileName = SafeName(file.FileName);
                old.ContentType = file.ContentType;
                old.FileSize = file.Length;
            }
            else
            {
                var document = new VerificationDocument
                {
                    Verification = entity, VerificationId = entity.Id,
                    DocumentType = type, FileKey = key, OriginalFileName = SafeName(file.FileName),
                    ContentType = file.ContentType, FileSize = file.Length
                };
                // IDs are allocated by the domain; mark new children Added explicitly.
                db.VerificationDocuments.Add(document);
            }
            entity.Revision++;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            committed = true;
            return Response(entity);
        }
        finally
        {
            if (!committed)
            {
                try { await storage.DeleteAsync(key, CancellationToken.None); }
                catch { logger.LogWarning("Compensating private document cleanup failed; provider reconciliation required."); }
            }
        }
    }

    public async Task<VerificationResponse> DeleteAsync(Guid accountId, Guid id, Guid documentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await LockedDraft(accountId, id, ct);
        var document = entity.Documents.SingleOrDefault(x => x.Id == documentId) ?? throw NotFound();
        QueueCleanup(document.FileKey);
        db.VerificationDocuments.Remove(document);
        entity.Documents.Remove(document);
        entity.CompletedStep = Math.Min(entity.CompletedStep, 2);
        entity.Revision++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(entity);
    }

    public async Task<PrivateDocumentContent> ReadAsync(Guid accountId, Guid id, Guid documentId, CancellationToken ct)
    {
        // Serialize against replacement/deletion until bytes are obtained.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAccount(accountId, ct);
        var entity = await Owned(accountId, id, ct);
        var document = entity.Documents.SingleOrDefault(x => x.Id == documentId) ?? throw NotFound();
        var bytes = await storage.ReadAsync(document.FileKey, ct);
        await tx.CommitAsync(ct);
        return new(bytes, document.ContentType, document.OriginalFileName);
    }

    public async Task<VerificationResponse> SubmitAsync(Guid accountId, Guid id, SubmitVerificationRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await LockedDraft(accountId, id, ct);
        RequireRevision(entity, request.Revision);
        if (!request.Confirmed) throw new VerificationException(400, "CONFIRMATION_REQUIRED", "Vui lòng xác nhận thông tin và tài liệu chính xác.");
        entity.VerificationData = LawyerVerificationValidation.Validate(entity.VerificationData, 3);
        LawyerVerificationValidation.RequireDocuments(entity.Documents.Select(x => x.DocumentType));
        entity.CompletedStep = 3;
        entity.Status = VerificationStatus.Pending;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.Revision++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(entity);
    }

    private void QueueCleanup(string key) => db.VerificationFileCleanups.Add(new() { FileKey = key });
    private static string SafeName(string name) => Path.GetFileName(name.Replace('\\', '/'));
    private static VerificationException NotFound() => new(404, "VERIFICATION_NOT_FOUND", "Không tìm thấy hồ sơ hoặc tài liệu.");
    private async Task RequireAccount(Guid id, CancellationToken ct)
    {
        if (!await db.Accounts.AnyAsync(x => x.Id == id && x.Status == AccountStatus.Active && x.DeletedAt == null && x.User != null, ct)) throw NotFound();
    }
    private async Task LockAccount(Guid id, CancellationToken ct)
    {
        // Account lock serializes initial creation and every mutation even before a draft exists.
        await db.Accounts.FromSqlInterpolated($"SELECT * FROM \"Accounts\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct);
        await RequireAccount(id, ct);
    }
    private async Task<ProfessionalVerification> Owned(Guid accountId, Guid id, CancellationToken ct) =>
        await db.ProfessionalVerifications.Include(x => x.Documents)
            .SingleOrDefaultAsync(x => x.Id == id && x.AccountId == accountId && x.Type == ProfessionalVerificationType.Lawyer, ct) ?? throw NotFound();
    private async Task<ProfessionalVerification> LockedDraft(Guid accountId, Guid id, CancellationToken ct)
    {
        await LockAccount(accountId, ct);
        var entity = await Owned(accountId, id, ct);
        if (entity.Status != VerificationStatus.Draft)
            throw new VerificationException(409, "VERIFICATION_READ_ONLY", "Hồ sơ đã gửi và không thể chỉnh sửa.");
        return entity;
    }
    private static void RequireRevision(ProfessionalVerification entity, int revision)
    {
        if (entity.Revision != revision) throw new VerificationException(409, "STALE_DRAFT", "Hồ sơ đã thay đổi. Hãy tải lại trước khi lưu.");
    }
    private static VerificationResponse Response(ProfessionalVerification x) => new(x.Id, x.Type, x.Status,
        x.VerificationData, x.CompletedStep, x.Revision, x.SubmittedAt, x.CreatedAt, x.UpdatedAt,
        x.Documents.OrderBy(d => d.DocumentType).Select(d => new VerificationDocumentResponse(
            d.Id, d.DocumentType, d.OriginalFileName, d.ContentType, d.FileSize, d.CreatedAt)).ToArray());
}
