namespace Sigov.Application.Storage;

public sealed record StoredFile(string Provider, string StorageKey, string HashSha256, long SizeBytes);

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(long tenantId, string originalName, Stream content, CancellationToken cancellationToken);
}

public interface IFileHashService
{
    Task<string> Sha256Async(Stream content, CancellationToken cancellationToken);
}

public interface IFileTypeValidator
{
    bool IsAllowed(string fileName, string contentType, long sizeBytes);
}

public interface IAntivirusScanner
{
    Task<bool> IsSafeAsync(Stream content, CancellationToken cancellationToken);
}

public interface IStorageKeyGenerator
{
    string Generate(long tenantId, string fileName);
}
