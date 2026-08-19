using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThinhPhat.Api.Shared.Services.Cloudinary;

public sealed class CloudinaryService : ICloudinaryService
{
    private readonly CloudinaryDotNet.Cloudinary _cloudinary;
    private readonly CloudinaryOptions _options;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IOptions<CloudinaryOptions> options, ILogger<CloudinaryService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var account = new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret);
        _cloudinary = new CloudinaryDotNet.Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<(string Url, string PublicId)> UploadImageAsync(
        IFormFile file,
        string? customFolder = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("File is empty or null.", nameof(file));
        }

        // Limit maximum image upload size to 5MB to protect 512MB RAM Render container
        if (file.Length > 5 * 1024 * 1024)
        {
            throw new InvalidOperationException("Image file size exceeds maximum limit of 5MB.");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only JPG, JPEG, PNG, and WEBP image formats are supported.");
        }

        // Stream direct to Cloudinary: zero large in-memory byte array allocation
        await using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = customFolder ?? _options.Folder,
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
        if (uploadResult.Error is not null)
        {
            _logger.LogError("Cloudinary upload failed: {Message}", uploadResult.Error.Message);
            throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
        }

        return (uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty, uploadResult.PublicId);
    }

    public async Task<bool> DeleteImageAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return false;

        var deletionParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deletionParams);

        return result.Result == "ok";
    }
}
