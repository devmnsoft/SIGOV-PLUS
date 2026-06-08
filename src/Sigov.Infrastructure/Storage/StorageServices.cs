using System.Security.Cryptography;
using Sigov.Application.Configuration;
using Sigov.Application.Storage;
using Microsoft.Extensions.Options;

namespace Sigov.Infrastructure.Storage;

public sealed class FileHashService : IFileHashService
{
    public async Task<string> Sha256Async(Stream content, CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return await ComputeSha256Async(content, cancellationToken).ConfigureAwait(false);
    }
    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        var originalPosition = stream.CanSeek ? stream.Position : 0;

        try
        {
            using var sha256 = SHA256.Create();
            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }
}

public sealed class StorageKeyGenerator : IStorageKeyGenerator
{
    public string Generate(long tenantId, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return $"tenants/{tenantId}/{DateTimeOffset.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
    }
}

public sealed class FileTypeValidator : IFileTypeValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".txt", ".csv" };
    private readonly IOptions<SigovOptions> _options;

    public FileTypeValidator(IOptions<SigovOptions> options) => _options = options;

    public bool IsAllowed(string fileName, string contentType, long sizeBytes) =>
        sizeBytes <= _options.Value.Storage.MaxUploadBytes && AllowedExtensions.Contains(Path.GetExtension(fileName));
}

public sealed class NoOpAntivirusScanner : IAntivirusScanner
{
    public Task<bool> IsSafeAsync(Stream content, CancellationToken cancellationToken) => Task.FromResult(true);
}

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly IOptions<SigovOptions> _options;
    private readonly IFileHashService _hashService;
    private readonly IStorageKeyGenerator _keyGenerator;

    public LocalFileStorageService(IOptions<SigovOptions> options, IFileHashService hashService, IStorageKeyGenerator keyGenerator)
    {
        _options = options;
        _hashService = hashService;
        _keyGenerator = keyGenerator;
    }

    public async Task<StoredFile> SaveAsync(long tenantId, string originalName, Stream content, CancellationToken cancellationToken)
    {
        var key = _keyGenerator.Generate(tenantId, originalName);
        var fullPath = Path.Combine(_options.Value.Storage.LocalPath, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var hash = await _hashService.Sha256Async(content, cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(fullPath);
        await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return new StoredFile("Local", key, hash, output.Length);
    }
}

public sealed class S3CompatibleStorageService : IFileStorageService
{
    public Task<StoredFile> SaveAsync(long tenantId, string originalName, Stream content, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Storage S3 compatível deve ser configurado com credenciais e endpoint em Production antes de habilitar uploads críticos.");
}
