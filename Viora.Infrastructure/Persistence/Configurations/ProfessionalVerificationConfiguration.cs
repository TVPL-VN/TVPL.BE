using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viora.Domain.Entities;

namespace Viora.Infrastructure.Persistence.Configurations;

internal sealed class ProfessionalVerificationConfiguration : IEntityTypeConfiguration<ProfessionalVerification>
{
    public void Configure(EntityTypeBuilder<ProfessionalVerification> b)
    {
        b.ToTable("ProfessionalVerifications", t =>
        {
            t.HasCheckConstraint("CK_ProfessionalVerifications_Type", "\"Type\" = 1");
            t.HasCheckConstraint("CK_ProfessionalVerifications_Status", "\"Status\" BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_ProfessionalVerifications_Step", "\"CompletedStep\" BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_ProfessionalVerifications_SubmittedAt", "(\"Status\" = 0 AND \"SubmittedAt\" IS NULL) OR (\"Status\" <> 0 AND \"SubmittedAt\" IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AccountId, x.Type }).IsUnique().HasFilter("\"Status\" IN (0, 1)")
            .HasDatabaseName("UX_ProfessionalVerifications_Active");
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.OwnsOne(x => x.VerificationData, data =>
        {
            data.ToJson("VerificationData");
            data.Property(x => x.SchemaVersion).HasJsonPropertyName("schemaVersion");
            data.OwnsOne(x => x.PublicProfile, p =>
            {
                p.HasJsonPropertyName("publicProfile");
                p.Property(x => x.ProfessionalTitle).HasJsonPropertyName("professionalTitle");
                p.Property(x => x.ProfessionalBio).HasJsonPropertyName("professionalBio");
                p.Property(x => x.YearsOfExperience).HasJsonPropertyName("yearsOfExperience");
                p.Property(x => x.Expertise).HasJsonPropertyName("expertise");
                p.Property(x => x.BarAssociation).HasJsonPropertyName("barAssociation");
                p.Property(x => x.OrganizationName).HasJsonPropertyName("organizationName");
                p.Property(x => x.Position).HasJsonPropertyName("position");
                p.Property(x => x.Location).HasJsonPropertyName("location");
            });
            data.OwnsOne(x => x.Verification, p =>
            {
                p.HasJsonPropertyName("verification");
                p.Property(x => x.PracticeCertificateNumber).HasJsonPropertyName("practiceCertificateNumber");
                p.Property(x => x.LawyerCardNumber).HasJsonPropertyName("lawyerCardNumber");
            });
        });
        b.Navigation(x => x.VerificationData).IsRequired();
    }
}

internal sealed class VerificationDocumentConfiguration : IEntityTypeConfiguration<VerificationDocument>
{
    public void Configure(EntityTypeBuilder<VerificationDocument> b)
    {
        b.ToTable("VerificationDocuments", t =>
        {
            t.HasCheckConstraint("CK_VerificationDocuments_Type", "\"DocumentType\" IN (1, 2, 99)");
            t.HasCheckConstraint("CK_VerificationDocuments_Size", "\"FileSize\" > 0 AND \"FileSize\" <= 10485760");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.FileKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.VerificationId, x.DocumentType }).IsUnique();
        b.HasOne(x => x.Verification).WithMany(x => x.Documents).HasForeignKey(x => x.VerificationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VerificationFileCleanupConfiguration : IEntityTypeConfiguration<VerificationFileCleanup>
{
    public void Configure(EntityTypeBuilder<VerificationFileCleanup> b)
    {
        b.ToTable("VerificationFileCleanups");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileKey).HasMaxLength(500).IsRequired();
    }
}
