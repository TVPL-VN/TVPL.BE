using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viora.Domain.Entities;

namespace Viora.Infrastructure.Persistence.Configurations;

internal sealed class StatutoryDocumentConfiguration : IEntityTypeConfiguration<StatutoryDocument>
{
    public void Configure(EntityTypeBuilder<StatutoryDocument> b)
    {
        b.ToTable("StatutoryDocuments"); b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(1000); b.Property(x => x.Number).HasMaxLength(100);
        b.Property(x => x.Summary).HasMaxLength(10000);
        b.Property(x => x.SourceUrl).HasMaxLength(2048); b.Property(x => x.FileUrl).HasMaxLength(2048);
        b.HasOne(x => x.Authority).WithMany().HasForeignKey(x => x.AuthorityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Number, x.AuthorityId }).IsUnique();
        b.HasIndex(x => new { x.IsPublished, x.IssuedOn }); b.HasIndex(x => x.EffectiveFrom);
        b.HasIndex(x => x.ExpiresOn); b.HasIndex(x => new { x.Type, x.IssuedOn });
    }
}
internal sealed class LegalFieldConfiguration : IEntityTypeConfiguration<LegalField>
{
    public void Configure(EntityTypeBuilder<LegalField> b) { b.ToTable("LegalFields"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(255); b.HasIndex(x => x.Name).IsUnique(); }
}
internal sealed class IssuingAuthorityConfiguration : IEntityTypeConfiguration<IssuingAuthority>
{
    public void Configure(EntityTypeBuilder<IssuingAuthority> b) { b.ToTable("IssuingAuthorities"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(255); b.HasIndex(x => x.Name).IsUnique(); }
}
internal sealed class StatutoryDocumentFieldConfiguration : IEntityTypeConfiguration<StatutoryDocumentField>
{
    public void Configure(EntityTypeBuilder<StatutoryDocumentField> b)
    {
        b.ToTable("StatutoryDocumentFields"); b.HasKey(x => new { x.DocumentId, x.FieldId });
        b.HasOne(x => x.Document).WithMany(x => x.Fields).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Field).WithMany().HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class LegalSectionConfiguration : IEntityTypeConfiguration<LegalSection>
{
    public void Configure(EntityTypeBuilder<LegalSection> b)
    {
        b.ToTable("LegalSections"); b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(1000); b.Property(x => x.Number).HasMaxLength(100); b.Property(x => x.Path).HasMaxLength(1000);
        b.HasOne(x => x.Document).WithMany(x => x.Sections).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DocumentId, x.ParentId, x.Order }); b.HasIndex(x => new { x.DocumentId, x.Path }).IsUnique();
    }
}
internal sealed class LegalSectionVersionConfiguration : IEntityTypeConfiguration<LegalSectionVersion>
{
    public void Configure(EntityTypeBuilder<LegalSectionVersion> b)
    {
        b.ToTable("LegalSectionVersions"); b.HasKey(x => x.Id);
        b.Property(x => x.Note).HasMaxLength(4000);
        b.HasOne(x => x.Section).WithMany(x => x.Versions).HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ChangedByDocument).WithMany().HasForeignKey(x => x.ChangedByDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SectionId, x.VersionNumber }).IsUnique();
        b.HasIndex(x => new { x.SectionId, x.ValidFrom }).IsUnique();
        b.HasIndex(x => new { x.SectionId, x.IsPublished, x.ValidFrom, x.ValidTo });
    }
}
internal sealed class StatutoryDocumentRelationConfiguration : IEntityTypeConfiguration<StatutoryDocumentRelation>
{
    public void Configure(EntityTypeBuilder<StatutoryDocumentRelation> b)
    {
        b.ToTable("StatutoryDocumentRelations"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RelatedDocument).WithMany().HasForeignKey(x => x.RelatedDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DocumentId, x.RelatedDocumentId, x.Type }).IsUnique();
    }
}
internal sealed class LegalSectionRelationConfiguration : IEntityTypeConfiguration<LegalSectionRelation>
{
    public void Configure(EntityTypeBuilder<LegalSectionRelation> b)
    {
        b.ToTable("LegalSectionRelations"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RelatedSection).WithMany().HasForeignKey(x => x.RelatedSectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SectionId, x.RelatedSectionId, x.Type }).IsUnique();
    }
}
