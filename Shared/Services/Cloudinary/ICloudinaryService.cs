using Microsoft.AspNetCore.Http;

namespace ThinhPhat.Api.Shared.Services.Cloudinary;

public interface ICloudinaryService
{
    Task<(string Url, string PublicId)> UploadImageAsync(IFormFile file, string? customFolder = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteImageAsync(string publicId, CancellationToken cancellationToken = default);
}
