using System.Data;
using Microsoft.EntityFrameworkCore;
using Viora.Application.Statutory;
using Viora.Domain.Entities;

namespace Viora.Infrastructure.Persistence.Repositories;

public sealed class StatutoryRepository(AppDbContext db) : IStatutoryRepository
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private static StatutoryException Invalid(string message) => new(400, message);
    private static StatutoryException Conflict(string message) => new(409, message);
    private static StatutoryException Missing() => new(404, "Không tìm thấy văn bản hoặc điều khoản.");
    private IQueryable<StatutoryDocument> Documents => db.StatutoryDocuments.Include(x => x.Authority).Include(x => x.Fields).ThenInclude(x => x.Field);
    private IQueryable<LegalSectionVersion> VisibleVersions(bool admin) => db.LegalSectionVersions
        .Where(x => admin || x.IsPublished && (x.ChangedByDocumentId == null || x.ChangedByDocument!.IsPublished));

    public async Task<StatutoryCatalog> CatalogAsync(CancellationToken t) => new(
        await db.LegalFields.AsNoTracking().OrderBy(x => x.Name).Select(x => new CatalogItem(x.Id, x.Name)).ToListAsync(t),
        await db.IssuingAuthorities.AsNoTracking().OrderBy(x => x.Name).Select(x => new CatalogItem(x.Id, x.Name)).ToListAsync(t));
    public async Task<CatalogItem> AddCatalogAsync(bool authority, string name, CancellationToken t)
    {
        name = name.Trim(); if (name.Length is < 1 or > 255) throw Invalid("Tên bắt buộc, tối đa 255 ký tự.");
        if (authority)
        {
            var old = await db.IssuingAuthorities.FirstOrDefaultAsync(x => x.Name == name, t);
            if (old is not null) return new(old.Id, old.Name);
            var item = new IssuingAuthority { Id = Guid.NewGuid(), Name = name }; db.Add(item); await db.SaveChangesAsync(t); return new(item.Id, item.Name);
        }
        else
        {
            var old = await db.LegalFields.FirstOrDefaultAsync(x => x.Name == name, t);
            if (old is not null) return new(old.Id, old.Name);
            var item = new LegalField { Id = Guid.NewGuid(), Name = name }; db.Add(item); await db.SaveChangesAsync(t); return new(item.Id, item.Name);
        }
    }
    public async Task<StatutoryPage> SearchAsync(StatutoryQuery r, bool admin, CancellationToken t)
    {
        if (r.Q?.Length > 300 || r.Page > 1000000 || r.Type is not null && !Enum.IsDefined(r.Type.Value) || r.Validity is not null && !Enum.IsDefined(r.Validity.Value)) throw Invalid("Bộ lọc không hợp lệ.");
        var today = Today;
        var query = Documents.AsNoTracking().Where(x => admin || x.IsPublished);
        if (admin && r.Published is not null) query = query.Where(x => x.IsPublished == r.Published);
        if (r.Type is not null) query = query.Where(x => x.Type == r.Type);
        if (r.AuthorityId is not null) query = query.Where(x => x.AuthorityId == r.AuthorityId);
        if (r.FieldId is not null) query = query.Where(x => x.Fields.Any(f => f.FieldId == r.FieldId));
        if (r.Year is not null)
        {
            if (r.Year is < 1 or > 9998) throw Invalid("Năm không hợp lệ.");
            var start = new DateOnly(r.Year.Value, 1, 1); var end = start.AddYears(1);
            query = query.Where(x => x.IssuedOn >= start && x.IssuedOn < end);
        }
        query = r.Validity switch
        {
            StatutoryValidity.Upcoming => query.Where(x => x.EffectiveFrom > today),
            StatutoryValidity.Expired => query.Where(x => x.EffectiveFrom <= today && x.ExpiresOn <= today),
            StatutoryValidity.PartiallyExpired => query.Where(x => x.EffectiveFrom <= today && (x.ExpiresOn == null || x.ExpiresOn > today) && x.PartiallyExpired),
            StatutoryValidity.Effective => query.Where(x => x.EffectiveFrom <= today && (x.ExpiresOn == null || x.ExpiresOn > today) && !x.PartiallyExpired),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(r.Q))
        {
            var text = r.Q.Trim(); var pattern = "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var visible = VisibleVersions(admin);
            query = query.Where(d => EF.Functions.ILike(d.Title, pattern) || EF.Functions.ILike(d.Number, pattern) ||
                EF.Functions.ToTsVector("simple", d.Title + " " + d.Number).Matches(EF.Functions.PlainToTsQuery("simple", text)) ||
                d.Sections.Any(s => (admin || !visible.Any(a => a.Section.DocumentId == d.Id && s.Path.StartsWith(a.Section.Path + "/") &&
                    a.ChangeType != LegalChangeType.Original && a.ValidFrom <= today &&
                    !visible.Any(n => n.SectionId == a.SectionId && n.ValidFrom <= today && n.ValidFrom > a.ValidFrom) &&
                    (a.ChangeType == LegalChangeType.Repealed || !visible.Any(c => c.SectionId == s.Id && c.ValidFrom <= today && c.ValidFrom > a.ValidFrom)))) &&
                    (EF.Functions.ILike(s.Title, pattern) || EF.Functions.ILike(s.Number, pattern) ||
                    EF.Functions.ILike((s.Type == LegalNodeType.Article ? "Điều " : s.Type == LegalNodeType.Clause ? "Khoản " : s.Type == LegalNodeType.Point ? "Điểm " : s.Type == LegalNodeType.Part ? "Phần " : s.Type == LegalNodeType.Chapter ? "Chương " : s.Type == LegalNodeType.Subsection ? "Tiểu mục " : "Mục ") + s.Number + " " + s.Title, pattern) ||
                    visible.Any(v => v.SectionId == s.Id && v.ValidFrom <= today &&
                        !visible.Any(n => n.SectionId == s.Id && n.ValidFrom <= today && n.ValidFrom > v.ValidFrom) &&
                        (EF.Functions.ILike(v.Content, pattern) || EF.Functions.ToTsVector("simple", v.Content).Matches(EF.Functions.PlainToTsQuery("simple", text)))))));
        }
        var total = await query.CountAsync(t); var page = Math.Max(1, r.Page); var size = Math.Clamp(r.PageSize, 1, 50);
        query = r.Sort switch { "oldest" => query.OrderBy(x => x.IssuedOn).ThenBy(x => x.Id), "title" => query.OrderBy(x => x.Title).ThenBy(x => x.Id), "effective" => query.OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Id), _ => query.OrderByDescending(x => x.IssuedOn).ThenBy(x => x.Id) };
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(t);
        return new(items.Select(Summary).ToList(), total, page, size);
    }
    public async Task<StatutoryDetail?> DetailAsync(Guid id, bool admin, DateOnly? at, CancellationToken t)
    {
        var document = await Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && (admin || x.IsPublished), t);
        if (document is null) return null;
        var sections = await db.LegalSections.AsNoTracking().Where(x => x.DocumentId == id).OrderBy(x => x.Order).ThenBy(x => x.Id).ToListAsync(t);
        var date = at ?? Today;
        var boundaryDate = at is null && document.EffectiveFrom > date ? document.EffectiveFrom : date;
        var visible = VisibleVersions(admin);
        // Read only the effective version and its next boundary, not every historical text.
        var versions = await visible.AsNoTracking().Include(x => x.ChangedByDocument)
            .Where(x => x.Section.DocumentId == id &&
                (x.ValidFrom <= boundaryDate && !visible.Any(n => n.SectionId == x.SectionId && n.ValidFrom <= boundaryDate && n.ValidFrom > x.ValidFrom) ||
                 x.ValidFrom > boundaryDate && !visible.Any(n => n.SectionId == x.SectionId && n.ValidFrom > boundaryDate && n.ValidFrom < x.ValidFrom)))
            .OrderBy(x => x.ValidFrom).ToListAsync(t);
        var rows = sections.Select(s => {
            var list = Timeline(versions.Where(v => v.SectionId == s.Id).ToList());
            var current = list.LastOrDefault(v => v.ValidFrom <= date) ?? (at is null && document.EffectiveFrom > date ? list.FirstOrDefault(v => v.ChangeType == LegalChangeType.Original) : null);
            return new SectionResponse(s.Id, s.ParentId, s.Type, s.Number, s.Title, s.Order, s.Path, current);
        }).ToList();
        if (!admin)
        {
            var index = rows.ToDictionary(x => x.Id);
            rows = rows.Where(row => {
                var parentId = row.ParentId;
                while (parentId is not null && index.TryGetValue(parentId.Value, out var parent))
                {
                    var change = parent.CurrentVersion;
                    if (change is not null && change.ChangeType != LegalChangeType.Original &&
                        (change.ChangeType == LegalChangeType.Repealed || row.CurrentVersion is null || change.ValidFrom >= row.CurrentVersion.ValidFrom)) return false;
                    parentId = parent.ParentId;
                }
                return true;
            }).ToList();
        }
        var relations = await db.StatutoryDocumentRelations.AsNoTracking().Include(x => x.Document).Include(x => x.RelatedDocument)
            .Where(x => (x.DocumentId == id || x.RelatedDocumentId == id) && (admin || x.Document.IsPublished && x.RelatedDocument.IsPublished)).ToListAsync(t);
        return new(Summary(document), rows, relations.Select(x => new RelationResponse(x.Id, x.DocumentId == id ? x.RelatedDocumentId : x.DocumentId,
            x.DocumentId == id ? x.RelatedDocument.Title : x.Document.Title, x.DocumentId == id ? x.RelatedDocument.Number : x.Document.Number, x.Type, x.DocumentId != id)).ToList());
    }
    public async Task<StatutorySummary> SaveAsync(Guid? id, Guid actor, SaveStatutoryRequest r, CancellationToken t)
    {
        var error = StatutoryRules.Validate(r); if (error is not null) throw Invalid(error);
        if (!await db.IssuingAuthorities.AnyAsync(x => x.Id == r.AuthorityId, t)) throw Invalid("Chọn cơ quan ban hành hợp lệ.");
        var fields = r.FieldIds?.Distinct().ToList() ?? new();
        if (fields.Count > 30 || await db.LegalFields.CountAsync(x => fields.Contains(x.Id), t) != fields.Count) throw Invalid("Lĩnh vực không hợp lệ.");
        if (await db.StatutoryDocuments.AnyAsync(x => x.Id != id && x.Number == r.Number.Trim() && x.AuthorityId == r.AuthorityId, t)) throw Conflict("Số hiệu đã tồn tại ở cơ quan này.");
        var d = id is null ? new StatutoryDocument { Id = Guid.NewGuid(), CreatedBy = actor } : await Documents.SingleOrDefaultAsync(x => x.Id == id, t) ?? throw Missing();
        if (id is null) db.Add(d);
        if ((d.IsPublished || d.PublishedAt != null || await db.LegalSectionVersions.AnyAsync(x => x.Section.DocumentId == d.Id, t)) && d.EffectiveFrom != default && d.EffectiveFrom != r.EffectiveFrom)
            throw Conflict("Không đổi ngày hiệu lực sau khi đã có nội dung. Hãy tạo phiên bản sửa đổi.");
        d.Title = r.Title.Trim(); d.Number = r.Number.Trim(); d.Type = r.Type; d.AuthorityId = r.AuthorityId;
        d.IssuedOn = r.IssuedOn; d.EffectiveFrom = r.EffectiveFrom; d.ExpiresOn = r.ExpiresOn; d.PartiallyExpired = r.PartiallyExpired;
        d.Summary = r.Summary?.Trim(); d.SourceUrl = r.SourceUrl?.Trim(); d.FileUrl = r.FileUrl?.Trim(); d.UpdatedBy = actor;
        foreach (var old in d.Fields.Where(x => !fields.Contains(x.FieldId)).ToList()) db.Remove(old);
        foreach (var field in fields.Where(f => !d.Fields.Any(x => x.FieldId == f))) d.Fields.Add(new() { DocumentId = d.Id, FieldId = field });
        await db.SaveChangesAsync(t); return Summary(await Documents.SingleAsync(x => x.Id == d.Id, t));
    }
    public async Task PublishAsync(Guid id, Guid actor, bool publish, CancellationToken t)
    {
        await using var tx = await db.Database.BeginTransactionAsync(t);
        await LockDocument(id, t);
        var d = await db.StatutoryDocuments.SingleOrDefaultAsync(x => x.Id == id, t) ?? throw Missing();
        if (publish && (!await db.LegalSections.AnyAsync(x => x.DocumentId == id && x.Type == LegalNodeType.Article, t) ||
            !await db.LegalSectionVersions.AnyAsync(x => x.Section.DocumentId == id && x.Section.Type >= LegalNodeType.Article && x.Content.Trim() != "", t))) throw Invalid("Cần có ít nhất một điều với nội dung trước khi công bố.");
        d.IsPublished = publish; if (publish) d.PublishedAt ??= DateTime.UtcNow; d.UpdatedBy = actor; await db.SaveChangesAsync(t);
        await tx.CommitAsync(t);
    }
    public async Task DeleteAsync(Guid id, CancellationToken t)
    {
        await using var tx = await db.Database.BeginTransactionAsync(t);
        await LockDocument(id, t);
        var d = await db.StatutoryDocuments.SingleOrDefaultAsync(x => x.Id == id, t) ?? throw Missing();
        if (d.PublishedAt != null || d.IsPublished || await db.LegalSections.AnyAsync(x => x.DocumentId == id, t) ||
            await db.StatutoryDocumentRelations.AnyAsync(x => x.DocumentId == id || x.RelatedDocumentId == id, t) ||
            await db.LegalSectionVersions.AnyAsync(x => x.ChangedByDocumentId == id, t)) throw Conflict("Không thể xóa văn bản có nội dung, lịch sử hoặc liên kết. Hãy ngừng công bố.");
        db.Remove(d); await db.SaveChangesAsync(t); await tx.CommitAsync(t);
    }
    public async Task<SectionResponse> SaveSectionAsync(Guid docId, Guid? id, Guid actor, SaveSectionRequest r, CancellationToken t)
    {
        await using var tx = await db.Database.BeginTransactionAsync(t);
        await LockDocument(docId, t);
        var d = await db.StatutoryDocuments.SingleOrDefaultAsync(x => x.Id == docId, t) ?? throw Missing();
        if (d.PublishedAt != null) throw Conflict("Cấu trúc đã công bố được bảo toàn. Dùng luồng sửa đổi nội dung.");
        if (r.Number.Length > 100 || string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 1000 || r.Content.Length > 1000000 || r.Order < 0) throw Invalid("Tiêu đề, số thứ tự hoặc nội dung không hợp lệ.");
        if (r.Number.Contains('/') || r.Number.Contains('\\') || r.Number.Contains(':') || r.Number.Any(char.IsControl))
            throw Invalid("Số/ký hiệu mục không được chứa ký tự phân tách đường dẫn hoặc ký tự điều khiển.");
        var parent = r.ParentId is null ? null : await db.LegalSections.SingleOrDefaultAsync(x => x.Id == r.ParentId && x.DocumentId == docId, t) ?? throw Invalid("Mục cha phải thuộc cùng văn bản.");
        if (!StatutoryRules.CanParent(parent?.Type, r.Type)) throw Invalid("Cấp mục không phù hợp với mục cha.");
        var s = id is null ? new LegalSection { Id = Guid.NewGuid(), DocumentId = docId } : await db.LegalSections.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id && x.DocumentId == docId, t) ?? throw Missing();
        if (id is not null && await db.LegalSections.AnyAsync(x => x.ParentId == id, t) && (s.ParentId != r.ParentId || s.Type != r.Type || s.Number != r.Number)) throw Conflict("Không đổi cấp, số hoặc cha khi mục có con. Sửa các mục con trước.");
        var cursor = parent; while (cursor is not null) { if (cursor.Id == s.Id) throw Invalid("Không thể tạo vòng lặp trong cấu trúc."); cursor = cursor.ParentId is null ? null : await db.LegalSections.FindAsync(new object[] { cursor.ParentId }, t); }
        s.ParentId = r.ParentId; s.Type = r.Type; s.Title = r.Title.Trim(); s.Number = r.Number.Trim(); s.Order = r.Order;
        s.Path = (parent?.Path is null ? "" : parent.Path + "/") + $"{(int)s.Type}:{(s.Number.Length > 0 ? s.Number : s.Id.ToString("N"))}";
        if (s.Path.Length > 1000) throw Invalid("Đường dẫn cấu trúc quá dài.");
        if (await db.LegalSections.AnyAsync(x => x.DocumentId == docId && x.Id != s.Id && x.Path == s.Path, t)) throw Conflict("Số/ký hiệu mục đã tồn tại trong cùng mục cha.");
        if (id is null) db.Add(s);
        var v = s.Versions.SingleOrDefault() ?? new LegalSectionVersion { Id = Guid.NewGuid(), SectionId = s.Id, VersionNumber = 1, ValidFrom = d.EffectiveFrom, IsPublished = true, CreatedBy = actor };
        v.Content = r.Content; if (!s.Versions.Contains(v)) s.Versions.Add(v);
        await db.SaveChangesAsync(t); await tx.CommitAsync(t);
        return new(s.Id, s.ParentId, s.Type, s.Number, s.Title, s.Order, s.Path, Version(v, null));
    }
    public async Task DeleteSectionAsync(Guid id, CancellationToken t)
    {
        await using var tx = await db.Database.BeginTransactionAsync(t);
        var documentId = await db.LegalSections.Where(x => x.Id == id).Select(x => (Guid?)x.DocumentId).SingleOrDefaultAsync(t) ?? throw Missing();
        await LockDocument(documentId, t);
        var s = await db.LegalSections.Include(x => x.Document).Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, t) ?? throw Missing();
        if (s.Document.PublishedAt != null || s.Versions.Count > 1 || await db.LegalSections.AnyAsync(x => x.ParentId == id, t) || await db.LegalSectionRelations.AnyAsync(x => x.SectionId == id || x.RelatedSectionId == id, t)) throw Conflict("Chỉ xóa mục lá của bản nháp chưa có lịch sử/liên kết.");
        db.RemoveRange(s.Versions); db.Remove(s); await db.SaveChangesAsync(t); await tx.CommitAsync(t);
    }
    public async Task<IReadOnlyList<SectionVersionResponse>> VersionsAsync(Guid id, bool admin, CancellationToken t)
    {
        if (!await db.LegalSections.AnyAsync(x => x.Id == id && (admin || x.Document.IsPublished), t)) throw Missing();
        return Timeline(await VisibleVersions(admin).AsNoTracking().Include(x => x.ChangedByDocument).Where(x => x.SectionId == id).OrderBy(x => x.ValidFrom).ToListAsync(t));
    }
    public async Task<SectionVersionResponse> AmendAsync(Guid id, Guid actor, AmendSectionRequest r, CancellationToken t)
    {
        if (!Enum.IsDefined(r.ChangeType) || r.ChangeType == LegalChangeType.Original || r.Content.Length > 1000000 || r.Note?.Length > 4000 || (r.ChangeType != LegalChangeType.Repealed && string.IsNullOrWhiteSpace(r.Content))) throw Invalid("Loại thay đổi hoặc nội dung không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(t);
        var s = await db.LegalSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, t) ?? throw Missing();
        await LockDocument(s.DocumentId, t);
        var doc = await db.StatutoryDocuments.SingleAsync(x => x.Id == s.DocumentId, t);
        if (doc.PublishedAt == null) throw Conflict("Bản nháp sửa nội dung trong cây. Chỉ tạo lịch sử cho văn bản đã công bố.");
        if (s.Type < LegalNodeType.Article) throw Invalid("Chọn Điều, Khoản hoặc Điểm để sửa đổi.");
        var cause = await db.StatutoryDocuments.SingleOrDefaultAsync(x => x.Id == r.ChangedByDocumentId, t) ?? throw Invalid("Văn bản sửa đổi không tồn tại.");
        if (cause.Id == s.DocumentId || r.ValidFrom < cause.EffectiveFrom || r.ValidFrom < doc.EffectiveFrom || r.ValidFrom == default) throw Invalid("Ngày thay đổi phải từ ngày hiệu lực của văn bản sửa đổi và khác văn bản gốc.");
        if (r.Publish && !cause.IsPublished) throw Conflict("Công bố văn bản sửa đổi trước khi áp dụng nội dung.");
        await CheckAmendmentHierarchy(s, r.ValidFrom, t);
        var versions = await db.LegalSectionVersions.Where(x => x.SectionId == id).OrderBy(x => x.ValidFrom).ToListAsync(t);
        if (versions.Count == 0 || r.ValidFrom <= versions.Last().ValidFrom) throw Conflict("Ngày phiên bản mới phải sau phiên bản cuối; không ghi đè lịch sử.");
        var v = new LegalSectionVersion { Id = Guid.NewGuid(), SectionId = id, VersionNumber = versions.Max(x => x.VersionNumber) + 1, Content = r.Content, ValidFrom = r.ValidFrom, IsPublished = r.Publish, ChangeType = r.ChangeType, ChangedByDocumentId = cause.Id, Note = r.Note, CreatedBy = actor };
        db.Add(v); if (v.IsPublished) CloseTimeline(versions.Append(v).ToList());
        doc.UpdatedBy = actor; doc.UpdatedAt = DateTime.UtcNow;
        if (!await db.StatutoryDocumentRelations.AnyAsync(x => x.DocumentId == cause.Id && x.RelatedDocumentId == doc.Id && x.Type == RelationFor(r.ChangeType), t))
            db.Add(new StatutoryDocumentRelation { Id = Guid.NewGuid(), DocumentId = cause.Id, RelatedDocumentId = doc.Id, Type = RelationFor(r.ChangeType) });
        await db.SaveChangesAsync(t); await tx.CommitAsync(t);
        return Version(v, null) with { ChangedByTitle = cause.Title, ChangedByNumber = cause.Number };
    }
    public async Task PublishVersionAsync(Guid id, Guid versionId, Guid actor, CancellationToken t)
    {
        await using var tx = await db.Database.BeginTransactionAsync(t);
        var s = await db.LegalSections.SingleOrDefaultAsync(x => x.Id == id, t) ?? throw Missing(); await LockDocument(s.DocumentId, t);
        var versions = await db.LegalSectionVersions.Include(x => x.ChangedByDocument).Where(x => x.SectionId == id).OrderBy(x => x.ValidFrom).ToListAsync(t);
        var v = versions.SingleOrDefault(x => x.Id == versionId) ?? throw Missing();
        if (!v.IsPublished) await CheckAmendmentHierarchy(s, v.ValidFrom, t);
        if (v.ChangedByDocumentId is not null && !v.ChangedByDocument!.IsPublished) throw Conflict("Văn bản sửa đổi chưa được công bố.");
        v.IsPublished = true; CloseTimeline(versions);
        var document = await db.StatutoryDocuments.SingleAsync(x => x.Id == s.DocumentId, t);
        document.UpdatedBy = actor; document.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(t); await tx.CommitAsync(t);
    }
    public async Task AddRelationAsync(Guid id, SaveRelationRequest r, CancellationToken t)
    {
        if (!Enum.IsDefined(r.Type) || id == r.RelatedDocumentId || await db.StatutoryDocuments.CountAsync(x => x.Id == id || x.Id == r.RelatedDocumentId, t) != 2) throw Invalid("Quan hệ hoặc văn bản liên quan không hợp lệ.");
        if (await db.StatutoryDocumentRelations.AnyAsync(x => x.DocumentId == id && x.RelatedDocumentId == r.RelatedDocumentId && x.Type == r.Type, t)) return;
        db.Add(new StatutoryDocumentRelation { Id = Guid.NewGuid(), DocumentId = id, RelatedDocumentId = r.RelatedDocumentId, Type = r.Type }); await db.SaveChangesAsync(t);
    }
    public async Task DeleteRelationAsync(Guid id, Guid relationId, CancellationToken t)
    {
        var r = await db.StatutoryDocumentRelations.SingleOrDefaultAsync(x => x.Id == relationId && x.DocumentId == id, t) ?? throw Missing();
        if (await db.LegalSectionVersions.AnyAsync(x => x.ChangedByDocumentId == id && x.Section.DocumentId == r.RelatedDocumentId, t)) throw Conflict("Quan hệ phục vụ lịch sử sửa đổi không được xóa.");
        db.Remove(r); await db.SaveChangesAsync(t);
    }
    public async Task AddSectionRelationAsync(Guid id, SaveSectionRelationRequest r, CancellationToken t)
    {
        if (!Enum.IsDefined(r.Type) || id == r.RelatedSectionId || await db.LegalSections.CountAsync(x => x.Id == id || x.Id == r.RelatedSectionId, t) != 2) throw Invalid("Liên kết điều khoản không hợp lệ.");
        if (await db.LegalSectionRelations.AnyAsync(x => x.SectionId == id && x.RelatedSectionId == r.RelatedSectionId && x.Type == r.Type, t)) return;
        db.Add(new LegalSectionRelation { Id = Guid.NewGuid(), SectionId = id, RelatedSectionId = r.RelatedSectionId, Type = r.Type }); await db.SaveChangesAsync(t);
    }
    private Task LockDocument(Guid id, CancellationToken t) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"StatutoryDocuments\" WHERE \"Id\" = {id} FOR UPDATE", t);
    private async Task CheckAmendmentHierarchy(LegalSection section, DateOnly date, CancellationToken t)
    {
        // A complete parent replacement owns its descendants. Do not append stale child text
        // below that replacement, or overwrite already scheduled child amendments.
        var parentId = section.ParentId;
        while (parentId is not null)
        {
            var parent = await db.LegalSections.SingleAsync(x => x.Id == parentId, t);
            var version = await db.LegalSectionVersions.Where(x => x.SectionId == parent.Id && x.IsPublished && x.ValidFrom <= date)
                .OrderByDescending(x => x.ValidFrom).FirstOrDefaultAsync(t);
            if (version is not null && version.ChangeType != LegalChangeType.Original)
                throw Conflict("Mục cha đã có nội dung thay thế hoặc bãi bỏ. Hãy sửa đổi toàn bộ mục cha để giữ nội dung nhất quán.");
            parentId = parent.ParentId;
        }
        if (await db.LegalSectionVersions.AnyAsync(x => x.Section.DocumentId == section.DocumentId &&
            x.Section.Path.StartsWith(section.Path + "/") && x.IsPublished && x.ValidFrom > date, t))
            throw Conflict("Mục con có sửa đổi áp dụng sau ngày đang chọn. Hãy chọn ngày sau phiên bản mục con cuối cùng.");
    }
    private static LegalRelationType RelationFor(LegalChangeType type) => type switch { LegalChangeType.Supplemented => LegalRelationType.Supplements, LegalChangeType.Replaced => LegalRelationType.Replaces, LegalChangeType.Repealed => LegalRelationType.Repeals, _ => LegalRelationType.Amends };
    private static void CloseTimeline(List<LegalSectionVersion> versions)
    {
        var published = versions.Where(x => x.IsPublished).OrderBy(x => x.ValidFrom).ToList();
        for (var i = 0; i < published.Count; i++) published[i].ValidTo = i + 1 < published.Count ? published[i + 1].ValidFrom.AddDays(-1) : null;
    }
    private static List<SectionVersionResponse> Timeline(List<LegalSectionVersion> versions) => versions.Select((v, i) => Version(v, i + 1 < versions.Count ? versions[i + 1].ValidFrom.AddDays(-1) : null)).ToList();
    private static SectionVersionResponse Version(LegalSectionVersion v, DateOnly? end) => new(v.Id, v.VersionNumber, v.Content, v.ValidFrom, end, v.IsPublished, v.ChangeType, v.ChangedByDocumentId, v.ChangedByDocument?.Title, v.ChangedByDocument?.Number, v.Note);
    private static StatutorySummary Summary(StatutoryDocument d) => new(d.Id, d.Title, d.Number, d.Type, new(d.AuthorityId, d.Authority.Name), d.IssuedOn, d.EffectiveFrom, d.ExpiresOn, d.PartiallyExpired, StatutoryRules.Validity(d.EffectiveFrom, d.ExpiresOn, d.PartiallyExpired, Today), d.Summary, d.SourceUrl, d.FileUrl, d.IsPublished, d.PublishedAt, d.Fields.Select(x => new CatalogItem(x.FieldId, x.Field.Name)).ToList());
}
