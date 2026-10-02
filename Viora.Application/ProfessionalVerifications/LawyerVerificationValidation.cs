using Viora.Domain.Entities;

namespace Viora.Application.ProfessionalVerifications;

public static class LawyerVerificationValidation
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public static readonly string[] Expertise = ["Dân sự", "Hình sự", "Đất đai", "Hôn nhân & Gia đình",
        "Lao động", "Doanh nghiệp", "Đầu tư", "M&A", "Hợp đồng", "Thuế", "Sở hữu trí tuệ", "Thương mại"];

    public static LawyerVerificationData Validate(LawyerVerificationData? data, int completedStep)
    {
        var errors = new Dictionary<string, string[]>();
        void Error(string field, string message) => errors[field] = [message];
        if (completedStep is < 0 or > 3) Error("completedStep", "Bước hoàn thành không hợp lệ.");
        if (data is null || data.SchemaVersion != 1 || data.PublicProfile is null || data.Verification is null)
            throw new VerificationException(400, "INVALID_DATA", "Cấu trúc hồ sơ không hợp lệ.");
        string Text(string? value, string key, int max, bool required)
        {
            var text = value?.Trim() ?? "";
            if (required && text.Length == 0) Error(key, "Vui lòng nhập thông tin này.");
            if (text.Length > max) Error(key, $"Tối đa {max} ký tự.");
            return text;
        }
        string[] Choices(string[]? values, string key, string[] choices, bool required)
        {
            var normalized = (values ?? []).Select(x => x?.Trim() ?? "").Distinct().ToArray();
            if (normalized.Length > choices.Length || normalized.Any(x => !choices.Contains(x))) Error(key, "Lựa chọn không hợp lệ.");
            if (required && normalized.Length == 0) Error(key, "Vui lòng chọn ít nhất một mục.");
            return normalized;
        }
        var p = data.PublicProfile;
        var publicRequired = completedStep >= 1;
        var result = new LawyerVerificationData
        {
            PublicProfile = new()
            {
                ProfessionalTitle = Text(p.ProfessionalTitle, "publicProfile.professionalTitle", 100, publicRequired),
                ProfessionalBio = Text(p.ProfessionalBio, "publicProfile.professionalBio", 2000, publicRequired),
                YearsOfExperience = p.YearsOfExperience,
                Expertise = Choices(p.Expertise, "publicProfile.expertise", Expertise, publicRequired),
                BarAssociation = Text(p.BarAssociation, "publicProfile.barAssociation", 200, publicRequired),
                OrganizationName = Text(p.OrganizationName, "publicProfile.organizationName", 200, false),
                Position = Text(p.Position, "publicProfile.position", 100, false),
                Location = Text(p.Location, "publicProfile.location", 200, publicRequired)
            },
            Verification = new()
            {
                PracticeCertificateNumber = Text(data.Verification.PracticeCertificateNumber, "verification.practiceCertificateNumber", 100, completedStep >= 2),
                LawyerCardNumber = Text(data.Verification.LawyerCardNumber, "verification.lawyerCardNumber", 100, completedStep >= 2)
            }
        };
        if ((publicRequired && p.YearsOfExperience is null) || p.YearsOfExperience is < 0 or > 80)
            Error("publicProfile.yearsOfExperience", "Nhập số năm kinh nghiệm từ 0 đến 80.");
        if (errors.Count > 0) throw new VerificationException(400, "VALIDATION_FAILED", "Vui lòng kiểm tra thông tin hồ sơ.", errors);
        return result;
    }

    public static void RequireDocuments(IEnumerable<VerificationDocumentType> types)
    {
        var set = types.ToHashSet();
        if (!set.Contains(VerificationDocumentType.PracticeCertificate) || !set.Contains(VerificationDocumentType.LawyerCard))
            throw new VerificationException(400, "DOCUMENTS_REQUIRED", "Cần chứng chỉ hành nghề và thẻ luật sư.");
    }

    public static async Task ValidateFileAsync(VerificationFile file, VerificationDocumentType type, CancellationToken ct)
    {
        if (!Enum.IsDefined(type)) throw new VerificationException(400, "INVALID_DOCUMENT_TYPE", "Loại tài liệu không hợp lệ.");
        if (!file.Content.CanSeek || file.Length is <= 0 or > MaxFileBytes || file.Content.Length != file.Length)
            throw new VerificationException(400, "INVALID_FILE_SIZE", "Tài liệu phải có dữ liệu và không vượt quá 10 MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var header = new byte[8];
        file.Content.Position = 0;
        var count = await file.Content.ReadAsync(header, ct);
        file.Content.Position = 0;
        var valid = (ext is ".jpg" or ".jpeg" && file.ContentType == "image/jpeg" && count >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
            || (ext == ".png" && file.ContentType == "image/png" && count == 8 && header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            || (ext == ".pdf" && file.ContentType == "application/pdf" && count >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        if (!valid) throw new VerificationException(400, "INVALID_FILE_FORMAT", "Chỉ hỗ trợ JPG, JPEG, PNG hoặc PDF đúng định dạng.");
        if (string.IsNullOrWhiteSpace(file.FileName) || file.FileName.Length > 255 || file.FileName.Any(char.IsControl))
            throw new VerificationException(400, "INVALID_FILE_NAME", "Tên tài liệu không hợp lệ.");
    }
}
