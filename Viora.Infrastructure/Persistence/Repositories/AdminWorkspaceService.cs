using Microsoft.EntityFrameworkCore;
using Viora.Application.Admin;
using Viora.Application.ProfessionalVerifications;
using Viora.Domain.Entities;

namespace Viora.Infrastructure.Persistence.Repositories;

public sealed class AdminWorkspaceService(AppDbContext db, IPrivateFileStorage storage) : IAdminWorkspaceService
{
    public Task<Guid?> ActiveAdminAsync(Guid accountId, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(x => x.AccountId == accountId && x.Account.Role == AccountRole.Admin && x.Account.Status == AccountStatus.Active && x.Account.DeletedAt == null)
        .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);

    private async Task RequireAdmin(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == id && x.Account.Role == AccountRole.Admin && x.Account.Status == AccountStatus.Active && x.Account.DeletedAt == null, ct))
            throw new VerificationException(403, "ADMIN_REQUIRED", "Bạn không có quyền quản trị hoặc tài khoản đã bị khóa.");
    }

    private IQueryable<AdminVerificationSummary> VerificationSummaries() => db.ProfessionalVerifications.AsNoTracking()
        .Where(x => x.Type == ProfessionalVerificationType.Lawyer && x.Account.User != null)
        .Select(x => new AdminVerificationSummary
        {
            Id = x.Id, UserId = x.Account.User!.Id, DisplayName = x.Account.User.DisplayName,
            AvatarUrl = x.Account.User.AvatarUrl, AccountStyle = x.Account.User.AccountStyle,
            BarAssociation = x.VerificationData.PublicProfile.BarAssociation,
            YearsOfExperience = x.VerificationData.PublicProfile.YearsOfExperience,
            Status = x.Status, SubmittedAt = x.SubmittedAt, Revision = x.Revision, DocumentCount = x.Documents.Count
        });

    public async Task<AdminPagedResponse<AdminVerificationSummary>> VerificationsAsync(int page, int pageSize, VerificationStatus status, string? keyword, CancellationToken ct)
    {
        if (status is not (VerificationStatus.Pending or VerificationStatus.Approved or VerificationStatus.Rejected))
            throw new VerificationException(400, "INVALID_STATUS", "Trạng thái hồ sơ không hợp lệ.");
        var query = VerificationSummaries().Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.DisplayName.ToLower().Contains(keyword.Trim().ToLower()));
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var count = await query.CountAsync(ct);
        return new(page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize),
            await query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct));
    }

    public async Task<AdminVerificationDetail> VerificationAsync(Guid id, CancellationToken ct)
    {
        var summary = await VerificationSummaries().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
        var entity = await db.ProfessionalVerifications.AsNoTracking().Include(x => x.Documents).Include(x => x.Reviewer).SingleAsync(x => x.Id == id, ct);
        return new(summary, entity.VerificationData,
            entity.Documents.OrderBy(x => x.DocumentType).Select(x => new VerificationDocumentResponse(x.Id, x.DocumentType, x.OriginalFileName, x.ContentType, x.FileSize, x.CreatedAt)).ToArray(),
            entity.ReviewerUserId, entity.Reviewer?.DisplayName, entity.ReviewedAt, entity.ReviewNote, entity.RejectionReason,
            await History("ProfessionalVerification", id, ct));
    }

    public async Task<AdminVerificationDetail> ReviewAsync(Guid adminId, Guid id, bool approve, AdminReviewRequest request, CancellationToken ct)
    {
        var reason = AdminDecisionRules.Reason(request.Reason, !approve);
        var note = AdminDecisionRules.Reason(request.Note, false);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        var accountId = await db.ProfessionalVerifications.Where(x => x.Id == id).Select(x => (Guid?)x.AccountId).SingleOrDefaultAsync(ct) ?? throw Missing();
        // Same account lock/order as owner mutations prevents upload/review races.
        await db.Accounts.FromSqlInterpolated($"SELECT * FROM \"Accounts\" WHERE \"Id\" = {accountId} FOR UPDATE").ToListAsync(ct);
        var entity = await db.ProfessionalVerifications.Include(x => x.Documents).Include(x => x.Account).ThenInclude(x => x.User).SingleAsync(x => x.Id == id, ct);
        AdminDecisionRules.RequirePending(entity.Status, entity.Revision, request.ExpectedRevision);
        var user = entity.Account.User ?? throw Missing();
        var oldStyle = user.AccountStyle;
        if (approve)
        {
            if (entity.Account.Status != AccountStatus.Active || entity.Account.DeletedAt is not null)
                throw new VerificationException(409, "ACCOUNT_INACTIVE", "Tài khoản người gửi hiện không hoạt động.");
            LawyerVerificationValidation.Validate(entity.VerificationData, 3);
            LawyerVerificationValidation.RequireDocuments(entity.Documents.Select(x => x.DocumentType));
            user.AccountStyle = AccountStyle.Lawyer;
        }
        entity.Status = approve ? VerificationStatus.Approved : VerificationStatus.Rejected;
        entity.ReviewerUserId = adminId; entity.ReviewedAt = DateTime.UtcNow;
        entity.ReviewNote = note.Length == 0 ? null : note;
        entity.RejectionReason = approve ? null : reason;
        entity.Revision++;
        Log(adminId, approve ? "ApproveProfessionalVerification" : "RejectProfessionalVerification", "ProfessionalVerification", id,
            $"Chờ duyệt → {(approve ? "Đã duyệt" : "Đã từ chối")}. {AccountStyleLabels.ToVietnamese(oldStyle)} → {AccountStyleLabels.ToVietnamese(user.AccountStyle)}. {(approve ? note : reason)}");
        db.Notifications.Add(new Notification { UserId = user.Id, NotificationType = NotificationType.System,
            Title = approve ? "Hồ sơ luật sư đã được phê duyệt" : "Hồ sơ xác minh cần chỉnh sửa",
            Content = approve ? "Bạn đã được xác nhận là Luật sư trên Cổng Luật Việt Nam." : reason,
            ReferenceType = NotificationReferenceType.User, ReferenceId = user.Id });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await VerificationAsync(id, ct);
    }

    public async Task<PrivateDocumentContent> DocumentAsync(Guid adminId, Guid id, Guid documentId, CancellationToken ct)
    {
        await RequireAdmin(adminId, ct);
        var document = await db.VerificationDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.VerificationId == id, ct) ?? throw Missing();
        var bytes = await storage.ReadAsync(document.FileKey, ct);
        Log(adminId, "ReadVerificationDocument", "ProfessionalVerification", id, $"Đã xem tài liệu: {document.OriginalFileName}");
        await db.SaveChangesAsync(ct);
        return new(bytes, document.ContentType, document.OriginalFileName);
    }

    public async Task<AdminUserContext> UserContextAsync(Guid id, CancellationToken ct)
    {
        var accountId = await db.Users.Where(x => x.Id == id).Select(x => (Guid?)x.AccountId).SingleOrDefaultAsync(ct) ?? throw Missing();
        var verificationId = await db.ProfessionalVerifications.Where(x => x.AccountId == accountId && x.Status != VerificationStatus.Draft)
            .OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        return new(await db.Posts.CountAsync(x => x.UserId == id && x.PostType == PostType.Article, ct),
            verificationId is null ? null : await VerificationSummaries().SingleAsync(x => x.Id == verificationId, ct), await History("User", id, ct),
            verificationId is null ? null : (await db.ProfessionalVerifications.AsNoTracking().SingleAsync(x => x.Id == verificationId, ct)).VerificationData.PublicProfile);
    }

    private IQueryable<AdminLogSummaryResponse> Logs(bool fullDescriptions = false) => db.AdminLogs.AsNoTracking().Select(x => new AdminLogSummaryResponse
    {
        Id = x.Id, AdminId = x.AdminId, AdminDisplayName = x.Admin.DisplayName, Action = x.Action,
        TargetType = x.TargetType, TargetId = x.TargetId,
        Description = !fullDescriptions && (x.Action == "AddMissingSection" || x.Action == "CorrectSectionData")
            ? "Dữ liệu số hóa văn bản pháp luật được cập nhật. Xem chi tiết để đọc lý do và dữ liệu trước/sau." : x.Description,
        CreatedAt = x.CreatedAt,
        TargetDisplayName = x.TargetType == "User" ? db.Users.Where(u => u.Id == x.TargetId).Select(u => u.DisplayName).FirstOrDefault()
            : x.TargetType == "ProfessionalVerification" ? db.ProfessionalVerifications.Where(v => v.Id == x.TargetId).Select(v => v.Account.User!.DisplayName).FirstOrDefault()
            : x.TargetType == "Post" || x.TargetType == "Video" ? db.Posts.Where(p => p.Id == x.TargetId).Select(p => p.User.DisplayName).FirstOrDefault() : null
    });
    private Task<List<AdminLogSummaryResponse>> History(string type, Guid id, CancellationToken ct) => Logs().Where(x => x.TargetType == type && x.TargetId == id).OrderByDescending(x => x.CreatedAt).Take(30).ToListAsync(ct);
    public async Task<AdminLogSummaryResponse> AuditAsync(Guid id, CancellationToken ct) => await Logs(true).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();

    public async Task<AdminWorkOverview> OverviewAsync(CancellationToken ct)
    {
        var pending = VerificationSummaries().Where(x => x.Status == VerificationStatus.Pending);
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var today = DateTime.UtcNow.AddHours(7).Date.AddHours(-7);
        var reports = db.Reports.AsNoTracking().Where(x => x.Status == ReportStatus.Pending);
        return new(await pending.CountAsync(ct), await pending.CountAsync(x => x.SubmittedAt < cutoff, ct),
            await reports.Select(x => (DateTime?)x.CreatedAt).MinAsync(ct),
            await db.Posts.CountAsync(x => x.PostType == PostType.Article && x.CreatedAt >= today, ct),
            await pending.OrderBy(x => x.SubmittedAt).Take(5).ToListAsync(ct),
            await reports.OrderBy(x => x.CreatedAt).Take(5).Select(x => new AdminReportSummaryResponse(x.Id, x.ReporterUserId, x.ReporterUser.DisplayName, x.ReporterUser.AvatarUrl, x.TargetId, x.TargetType, x.Reason, x.Status, x.Description, x.CreatedAt)).ToListAsync(ct),
            await Logs().OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync(ct));
    }

    public async Task UserActionAsync(Guid adminId, Guid id, string action, AdminUserDecisionRequest request, AccountStatus? status, AccountStyle? style, CancellationToken ct)
    {
        var reason = AdminDecisionRules.Reason(request.Reason);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        var accountId = await db.Users.Where(x => x.Id == id).Select(x => (Guid?)x.AccountId).SingleOrDefaultAsync(ct) ?? throw Missing();
        await db.Accounts.FromSqlInterpolated($"SELECT * FROM \"Accounts\" WHERE \"Id\" = {accountId} FOR UPDATE").ToListAsync(ct);
        var user = await db.Users.Include(x => x.Account).SingleAsync(x => x.Id == id, ct);
        if (user.Account.Role == AccountRole.Admin) throw new VerificationException(403, "STAFF_PROTECTED", "Không thể thay đổi tài khoản quản trị bằng thao tác này.");
        if (action == "Status")
        {
            if (status is not (AccountStatus.Active or AccountStatus.Banned)) throw Invalid();
            if (request.ExpectedStatus != user.Account.Status || user.Account.Status == AccountStatus.Deleted || user.Account.Status == status) throw Conflict();
            var old = user.Account.Status; user.Account.Status = status.Value;
            Log(adminId, status == AccountStatus.Banned ? "BanUser" : "UnbanUser", "User", id, $"{old} → {status}. {reason}");
        }
        else if (action == "AccountStyle")
        {
            if (user.Account.Status == AccountStatus.Deleted || user.Account.DeletedAt is not null) throw Conflict();
            if (style is null || !Enum.IsDefined(style.Value)) throw Invalid();
            if (request.ExpectedAccountStyle != user.AccountStyle || user.AccountStyle == style) throw Conflict();
            var old = user.AccountStyle; user.AccountStyle = style.Value;
            Log(adminId, "UpdateUserAccountStyle", "User", id, $"{AccountStyleLabels.ToVietnamese(old)} → {AccountStyleLabels.ToVietnamese(style.Value)}. {reason}");
            db.Notifications.Add(new Notification { UserId = id, NotificationType = NotificationType.System, Title = "Loại tài khoản đã được cập nhật", Content = AccountStyleLabels.ToVietnamese(style.Value), ReferenceType = NotificationReferenceType.User, ReferenceId = id });
        }
        else if (action != "RevokeSessions") throw Invalid();
        if (action == "RevokeSessions" || status == AccountStatus.Banned)
        {
            user.Account.SessionVersion++;
            var tokens = await db.RefreshTokens.Where(x => x.AccountId == accountId && x.RevokedAt == null).ToListAsync(ct);
            foreach (var token in tokens) token.RevokedAt = DateTime.UtcNow;
            if (action == "RevokeSessions") Log(adminId, "RevokeUserSessions", "User", id, reason);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task<Post> LockPost(Guid id, CancellationToken ct) => (await db.Posts.FromSqlInterpolated($"SELECT * FROM \"Posts\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw Missing();
    private static void CheckPost(Post post, PostStatus? expectedStatus, DateTime? expectedUpdatedAt)
    {
        if (post.Status != expectedStatus || post.UpdatedAt != expectedUpdatedAt) throw Conflict();
    }
    public async Task ModerateAsync(Guid adminId, Guid id, PostType type, PostStatus status, AdminContentDecisionRequest request, CancellationToken ct)
    {
        var reason = AdminDecisionRules.Reason(request.Reason);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        var post = await LockPost(id, ct);
        if (post.PostType != type) throw Missing();
        CheckPost(post, request.ExpectedStatus, request.ExpectedUpdatedAt);
        AdminDecisionRules.ContentTransition(post.Status, status);
        SetPost(adminId, post, status, reason);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private void SetPost(Guid adminId, Post post, PostStatus status, string reason)
    {
        var old = post.Status; post.Status = status; post.DeletedAt = status == PostStatus.Deleted ? DateTime.UtcNow : null;
        Log(adminId, status == PostStatus.Hidden ? "HideContent" : status == PostStatus.Deleted ? "SoftDeleteContent" : "RestoreContent", "Post", post.Id, $"{old} → {status}. {reason}");
    }
    public async Task ReportDecisionAsync(Guid adminId, Guid id, bool approve, AdminReportDecisionRequest request, CancellationToken ct)
    {
        var reason = AdminDecisionRules.Reason(request.Reason);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        var report = (await db.Reports.FromSqlInterpolated($"SELECT * FROM \"Reports\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw Missing();
        if (report.Status != ReportStatus.Pending || report.Status != request.ExpectedStatus) throw Conflict();
        if (!string.IsNullOrEmpty(request.Action) && request.Action != "None")
        {
            if (!approve || report.TargetType != ReportTargetType.Post || request.Action is not ("HidePost" or "DeletePost")) throw Invalid();
            var post = await LockPost(report.TargetId, ct);
            CheckPost(post, request.ExpectedTargetStatus, request.ExpectedTargetUpdatedAt);
            var next = request.Action == "HidePost" ? PostStatus.Hidden : PostStatus.Deleted;
            AdminDecisionRules.ContentTransition(post.Status, next); SetPost(adminId, post, next, reason);
        }
        report.Status = approve ? ReportStatus.Approved : ReportStatus.Rejected; report.ReviewedBy = adminId; report.ReviewedAt = DateTime.UtcNow;
        Log(adminId, approve ? "ApproveReport" : "RejectReport", "Report", id, $"Chờ xử lý → {(approve ? "Đã xác nhận" : "Đã bỏ qua")}. {reason}");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private void Log(Guid admin, string action, string type, Guid id, string? description) => db.AdminLogs.Add(new AdminLog { AdminId = admin, Action = action, TargetType = type, TargetId = id, Description = description });
    private static VerificationException Missing() => new(404, "NOT_FOUND", "Không tìm thấy dữ liệu được yêu cầu.");
    private static VerificationException Conflict() => new(409, "STALE_STATE", "Dữ liệu vừa thay đổi. Hãy tải lại trước khi thao tác.");
    private static VerificationException Invalid() => new(400, "INVALID_ACTION", "Thao tác không hợp lệ.");
}
