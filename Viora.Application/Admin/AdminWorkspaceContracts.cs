using Viora.Application.ProfessionalVerifications;
using Viora.Domain.Entities;

namespace Viora.Application.Admin;

public static class AdminDecisionRules
{
    public static bool IsActiveAdmin(AccountRole role, AccountStatus status) => role == AccountRole.Admin && status == AccountStatus.Active;
    public static string Reason(string? value, bool required = true)
    {
        var text = value?.Trim() ?? "";
        if ((required && text.Length == 0) || text.Length > 2000)
            throw new VerificationException(400, "REASON_REQUIRED", "Nhập lý do, tối đa 2.000 ký tự.");
        return text;
    }
    public static void RequirePending(VerificationStatus status, int revision, int expected)
    {
        if (status != VerificationStatus.Pending || revision != expected)
            throw new VerificationException(409, "STALE_REVIEW", "Hồ sơ này vừa thay đổi hoặc đã được quản trị viên khác xử lý.");
    }
    public static void ContentTransition(PostStatus current, PostStatus next)
    {
        var valid = next switch
        {
            PostStatus.Hidden => current == PostStatus.Published,
            PostStatus.Published => current is PostStatus.Hidden or PostStatus.Deleted,
            PostStatus.Deleted => current is PostStatus.Published or PostStatus.Hidden,
            _ => false
        };
        if (!valid) throw new VerificationException(409, "INVALID_STATE", "Thao tác không phù hợp với trạng thái nội dung hiện tại.");
    }
}

public sealed record AdminVerificationSummary(Guid Id, Guid UserId, string DisplayName, string? AvatarUrl,
    AccountStyle AccountStyle, string BarAssociation, int? YearsOfExperience, VerificationStatus Status,
    DateTime? SubmittedAt, int Revision, int DocumentCount)
{
    public AdminVerificationSummary() : this(default, default, "", null, default, "", null, default, null, 0, 0) { }
}
public sealed record AdminVerificationDetail(AdminVerificationSummary Summary, LawyerVerificationData VerificationData,
    IReadOnlyList<VerificationDocumentResponse> Documents, Guid? ReviewerUserId, string? ReviewerName,
    DateTime? ReviewedAt, string? ReviewNote, string? RejectionReason, IReadOnlyList<AdminLogSummaryResponse> History);
public sealed record AdminReviewRequest(int ExpectedRevision, string? Reason = null, string? Note = null);
public sealed record AdminContentDecisionRequest(PostStatus ExpectedStatus, DateTime ExpectedUpdatedAt, string? Reason);
public sealed record AdminReportDecisionRequest(ReportStatus ExpectedStatus, string? Reason, string? Action = null,
    PostStatus? ExpectedTargetStatus = null, DateTime? ExpectedTargetUpdatedAt = null);
public sealed record AdminUserDecisionRequest(string? Reason, AccountStatus? ExpectedStatus = null, AccountStyle? ExpectedAccountStyle = null);
public sealed record AdminUserContext(int ArticleCount, AdminVerificationSummary? ProfessionalVerification,
    IReadOnlyList<AdminLogSummaryResponse> History, LawyerPublicProfile? PublicProfile = null);
public sealed record AdminWorkOverview(int PendingVerificationCount, int OverdueVerificationCount,
    DateTime? OldestPendingReportAt, int TodayArticleCount, IReadOnlyList<AdminVerificationSummary> Verifications,
    IReadOnlyList<AdminReportSummaryResponse> Reports, IReadOnlyList<AdminLogSummaryResponse> Activity);

public interface IAdminWorkspaceService
{
    Task<Guid?> ActiveAdminAsync(Guid accountId, CancellationToken ct);
    Task<AdminWorkOverview> OverviewAsync(CancellationToken ct);
    Task<AdminPagedResponse<AdminVerificationSummary>> VerificationsAsync(int page, int pageSize, VerificationStatus status, string? keyword, CancellationToken ct);
    Task<AdminVerificationDetail> VerificationAsync(Guid id, CancellationToken ct);
    Task<AdminVerificationDetail> ReviewAsync(Guid adminId, Guid id, bool approve, AdminReviewRequest request, CancellationToken ct);
    Task<PrivateDocumentContent> DocumentAsync(Guid adminId, Guid id, Guid documentId, CancellationToken ct);
    Task<AdminUserContext> UserContextAsync(Guid id, CancellationToken ct);
    Task<AdminLogSummaryResponse> AuditAsync(Guid id, CancellationToken ct);
    Task UserActionAsync(Guid adminId, Guid id, string action, AdminUserDecisionRequest request, AccountStatus? status, AccountStyle? style, CancellationToken ct);
    Task ModerateAsync(Guid adminId, Guid id, PostType type, PostStatus status, AdminContentDecisionRequest request, CancellationToken ct);
    Task ReportDecisionAsync(Guid adminId, Guid id, bool approve, AdminReportDecisionRequest request, CancellationToken ct);
}
