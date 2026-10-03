using Viora.Domain.Entities;

namespace Viora.Application.Statutory;

public sealed record CatalogItem(Guid Id, string Name);
public sealed record StatutoryCatalog(IReadOnlyList<CatalogItem> Fields, IReadOnlyList<CatalogItem> Authorities);
public sealed class StatutoryQuery
{
    public string? Q { get; set; }
    public StatutoryType? Type { get; set; }
    public StatutoryValidity? Validity { get; set; }
    public Guid? FieldId { get; set; }
    public Guid? AuthorityId { get; set; }
    public int? Year { get; set; }
    public bool? Published { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}
public sealed record StatutoryPage(IReadOnlyList<StatutorySummary> Items, int Total, int Page, int PageSize);
public sealed record StatutorySummary(Guid Id, string Title, string Number, StatutoryType Type,
    CatalogItem Authority, DateOnly IssuedOn, DateOnly EffectiveFrom, DateOnly? ExpiresOn,
    bool PartiallyExpired, StatutoryValidity Validity, string? Summary, string? SourceUrl, string? FileUrl,
    bool IsPublished, DateTime? PublishedAt, IReadOnlyList<CatalogItem> Fields);
public sealed record SectionVersionResponse(Guid Id, int VersionNumber, string Content, DateOnly ValidFrom,
    DateOnly? ValidTo, bool IsPublished, LegalChangeType ChangeType, Guid? ChangedByDocumentId,
    string? ChangedByTitle, string? ChangedByNumber, string? Note);
public sealed record SectionResponse(Guid Id, Guid? ParentId, LegalNodeType Type, string Number, string Title,
    int Order, string Path, SectionVersionResponse? CurrentVersion);
public sealed record RelationResponse(Guid Id, Guid RelatedDocumentId, string Title, string Number,
    LegalRelationType Type, bool Incoming);
public sealed record StatutoryDetail(StatutorySummary Document, IReadOnlyList<SectionResponse> Sections, IReadOnlyList<RelationResponse> Relations);
public sealed record SaveStatutoryRequest(string Title, string Number, StatutoryType Type, Guid AuthorityId,
    DateOnly IssuedOn, DateOnly EffectiveFrom, DateOnly? ExpiresOn, bool PartiallyExpired,
    string? Summary, string? SourceUrl, string? FileUrl, IReadOnlyList<Guid>? FieldIds);
public sealed record SaveSectionRequest(Guid? ParentId, LegalNodeType Type, string Number, string Title, int Order, string Content);
public sealed record AmendSectionRequest(string Content, DateOnly ValidFrom, Guid ChangedByDocumentId,
    LegalChangeType ChangeType, string? Note, bool Publish);
public sealed record SaveRelationRequest(Guid RelatedDocumentId, LegalRelationType Type);
public sealed record SaveSectionRelationRequest(Guid RelatedSectionId, LegalRelationType Type);
public sealed class StatutoryException(int status, string message) : Exception(message) { public int Status { get; } = status; }

public interface IStatutoryRepository
{
    Task<StatutoryCatalog> CatalogAsync(CancellationToken token);
    Task<CatalogItem> AddCatalogAsync(bool authority, string name, CancellationToken token);
    Task<StatutoryPage> SearchAsync(StatutoryQuery query, bool admin, CancellationToken token);
    Task<StatutoryDetail?> DetailAsync(Guid id, bool admin, DateOnly? at, CancellationToken token);
    Task<StatutorySummary> SaveAsync(Guid? id, Guid actor, SaveStatutoryRequest request, CancellationToken token);
    Task PublishAsync(Guid id, Guid actor, bool publish, CancellationToken token);
    Task DeleteAsync(Guid id, CancellationToken token);
    Task<SectionResponse> SaveSectionAsync(Guid docId, Guid? id, Guid actor, SaveSectionRequest request, CancellationToken token);
    Task DeleteSectionAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<SectionVersionResponse>> VersionsAsync(Guid sectionId, bool admin, CancellationToken token);
    Task<SectionVersionResponse> AmendAsync(Guid sectionId, Guid actor, AmendSectionRequest request, CancellationToken token);
    Task PublishVersionAsync(Guid sectionId, Guid versionId, Guid actor, CancellationToken token);
    Task AddRelationAsync(Guid docId, SaveRelationRequest request, CancellationToken token);
    Task DeleteRelationAsync(Guid docId, Guid relationId, CancellationToken token);
    Task AddSectionRelationAsync(Guid sectionId, SaveSectionRelationRequest request, CancellationToken token);
}
public static class StatutoryRules
{
    public static bool CanParent(LegalNodeType? parent, LegalNodeType child) => Enum.IsDefined(child) &&
        (parent is null ? child <= LegalNodeType.Article : parent < child &&
        (child == LegalNodeType.Point ? parent == LegalNodeType.Clause :
         child == LegalNodeType.Clause ? parent == LegalNodeType.Article : parent < LegalNodeType.Article));
    public static StatutoryValidity Validity(DateOnly start, DateOnly? end, bool partial, DateOnly today) =>
        start > today ? StatutoryValidity.Upcoming : end <= today ? StatutoryValidity.Expired :
        partial ? StatutoryValidity.PartiallyExpired : StatutoryValidity.Effective;
    public static bool SafeUrl(string? value) => string.IsNullOrWhiteSpace(value) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http") && string.IsNullOrEmpty(uri.UserInfo);
    public static string? Validate(SaveStatutoryRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 1000) return "Tên văn bản bắt buộc, tối đa 1000 ký tự.";
        if (string.IsNullOrWhiteSpace(r.Number) || r.Number.Length > 100) return "Số hiệu bắt buộc, tối đa 100 ký tự.";
        if (!Enum.IsDefined(r.Type)) return "Loại văn bản không hợp lệ.";
        if (r.IssuedOn == default || r.EffectiveFrom == default || r.EffectiveFrom < r.IssuedOn) return "Ngày hiệu lực phải từ ngày ban hành trở đi.";
        if (r.ExpiresOn is not null && r.ExpiresOn < r.EffectiveFrom) return "Ngày hết hiệu lực không thể trước ngày có hiệu lực.";
        if (!SafeUrl(r.SourceUrl) || !SafeUrl(r.FileUrl)) return "Liên kết phải dùng http hoặc https.";
        if (r.Summary?.Length > 10000 || r.SourceUrl?.Length > 2048 || r.FileUrl?.Length > 2048) return "Thông tin vượt độ dài cho phép.";
        return null;
    }
}
