namespace Viora.Domain.Entities;

public enum ProfessionalVerificationType : short { Lawyer = 1 }
public enum VerificationStatus : short { Draft = 0, Pending = 1, Approved = 2, Rejected = 3 }
public enum VerificationDocumentType : short { PracticeCertificate = 1, LawyerCard = 2, Other = 99 }

public sealed class ProfessionalVerification : AuditableEntity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public ProfessionalVerificationType Type { get; set; } = ProfessionalVerificationType.Lawyer;
    public VerificationStatus Status { get; set; } = VerificationStatus.Draft;
    public LawyerVerificationData VerificationData { get; set; } = new();
    public int CompletedStep { get; set; }
    public int Revision { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public ICollection<VerificationDocument> Documents { get; set; } = [];
}

public sealed class LawyerVerificationData
{
    public int SchemaVersion { get; set; } = 1;
    public LawyerPublicProfile PublicProfile { get; set; } = new();
    public LawyerPracticeVerification Verification { get; set; } = new();
}

public sealed class LawyerPublicProfile
{
    public string ProfessionalTitle { get; set; } = "Luật sư";
    public string ProfessionalBio { get; set; } = "";
    public int? YearsOfExperience { get; set; }
    public string[] Expertise { get; set; } = [];
    public string BarAssociation { get; set; } = "";
    public string? OrganizationName { get; set; }
    public string? Position { get; set; }
    public string Location { get; set; } = "";
}

public sealed class LawyerPracticeVerification
{
    public string PracticeCertificateNumber { get; set; } = "";
    public string LawyerCardNumber { get; set; } = "";
}

public sealed class VerificationDocument : AuditableEntity
{
    public Guid VerificationId { get; set; }
    public ProfessionalVerification Verification { get; set; } = null!;
    public VerificationDocumentType DocumentType { get; set; }
    public string FileKey { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
}
