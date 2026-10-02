using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using Viora.Application.ProfessionalVerifications;

namespace Viora.Infrastructure.Media;

public sealed class CloudinaryPrivateFileStorage : IPrivateFileStorage
{
    private readonly Cloudinary cloudinary;
    private readonly IHttpClientFactory http;
    public CloudinaryPrivateFileStorage(IOptions<CloudinaryOptions> options, IHttpClientFactory http)
    {
        var o = options.Value;
        cloudinary = new(new Account(o.CloudName, o.ApiKey, o.ApiSecret));
        cloudinary.Api.Secure = true;
        this.http = http;
    }

    public async Task<string> UploadAsync(Guid accountId, Guid verificationId, VerificationFile file, CancellationToken ct)
    {
        var key = $"verification/{accountId:N}/{verificationId:N}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        try
        {
            var result = await cloudinary.UploadAsync(new RawUploadParams
            {
                PublicId = key, Type = "authenticated", Overwrite = false,
                UseFilename = false, UniqueFilename = false,
                File = new FileDescription("document" + Path.GetExtension(key), file.Content)
            }, "raw", ct);
            if (result.Error is not null || result.PublicId != key || result.Type != "authenticated" || result.Bytes != file.Length)
                throw new VerificationException(503, "PRIVATE_UPLOAD_FAILED", "Không thể lưu tài liệu riêng tư. Vui lòng thử lại.");
            return key;
        }
        catch
        {
            // Upload may have reached the provider even if the response was cancelled.
            try { await DeleteAsync(key, CancellationToken.None); } catch { /* Original error takes precedence. */ }
            throw;
        }
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        var url = cloudinary.DownloadPrivate(key, attachment: false, type: "authenticated",
            resourceType: "raw", expiresAt: DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());
        // A dedicated client avoids logging signed URLs/credentials through default HTTP logging.
        using var client = http.CreateClient("verification-private");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new VerificationException(503, "PRIVATE_READ_FAILED", "Không thể tải tài liệu. Vui lòng thử lại.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int length;
        while ((length = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + length > LawyerVerificationValidation.MaxFileBytes)
                throw new VerificationException(503, "PRIVATE_READ_FAILED", "Không thể tải tài liệu.");
            await output.WriteAsync(buffer.AsMemory(0, length), ct);
        }
        return output.ToArray();
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await cloudinary.DestroyAsync(new DeletionParams(key)
        { Type = "authenticated", ResourceType = ResourceType.Raw, Invalidate = true });
        if (result.Error is not null || result.Result is not ("ok" or "not found"))
            throw new InvalidOperationException("Private document cleanup failed.");
    }
}
