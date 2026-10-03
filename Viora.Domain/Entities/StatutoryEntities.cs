namespace Viora.Domain.Entities;

public enum StatutoryType : short { Law, Code, Decree, Circular, Resolution, Decision, Directive, Other }
public enum StatutoryValidity : short { Upcoming, Effective, PartiallyExpired, Expired }
public enum LegalNodeType : short { Part, Chapter, Section, Subsection, Article, Clause, Point }
public enum LegalChangeType : short { Original, Amended, Supplemented, Replaced, Repealed }
public enum LegalRelationType : short { Amends, Supplements, Replaces, Repeals, Guides, Implements, References, Related }

public sealed class StatutoryDocument
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Number { get; set; } = "";
    public StatutoryType Type { get; set; }
    public Guid AuthorityId { get; set; }
    public IssuingAuthority Authority { get; set; } = null!;
    public DateOnly IssuedOn { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public bool PartiallyExpired { get; set; }
    public string? Summary { get; set; }
    public string? SourceUrl { get; set; }
    public string? FileUrl { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<StatutoryDocumentField> Fields { get; set; } = new List<StatutoryDocumentField>();
    public ICollection<LegalSection> Sections { get; set; } = new List<LegalSection>();
}
public sealed class LegalField { public Guid Id { get; set; } public string Name { get; set; } = ""; }
public sealed class IssuingAuthority { public Guid Id { get; set; } public string Name { get; set; } = ""; }
public sealed class StatutoryDocumentField
{
    public Guid DocumentId { get; set; }
    public StatutoryDocument Document { get; set; } = null!;
    public Guid FieldId { get; set; }
    public LegalField Field { get; set; } = null!;
}
public sealed class LegalSection
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public StatutoryDocument Document { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public LegalSection? Parent { get; set; }
    public LegalNodeType Type { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public string Path { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<LegalSectionVersion> Versions { get; set; } = new List<LegalSectionVersion>();
}
public sealed class LegalSectionVersion
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public LegalSection Section { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string Content { get; set; } = "";
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsPublished { get; set; }
    public LegalChangeType ChangeType { get; set; }
    public Guid? ChangedByDocumentId { get; set; }
    public StatutoryDocument? ChangedByDocument { get; set; }
    public string? Note { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public sealed class StatutoryDocumentRelation
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public StatutoryDocument Document { get; set; } = null!;
    public Guid RelatedDocumentId { get; set; }
    public StatutoryDocument RelatedDocument { get; set; } = null!;
    public LegalRelationType Type { get; set; }
}
public sealed class LegalSectionRelation
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public LegalSection Section { get; set; } = null!;
    public Guid RelatedSectionId { get; set; }
    public LegalSection RelatedSection { get; set; } = null!;
    public LegalRelationType Type { get; set; }
}
