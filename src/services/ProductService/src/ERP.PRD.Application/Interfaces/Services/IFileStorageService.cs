namespace ERP.PRD.Application.Interfaces.Services;

/// <summary>
/// Abstraction over the blob storage backend used for product image uploads
/// (Azure Blob Storage in production, the Azurite emulator locally). Kept
/// framework-agnostic so Application-layer handlers never reference the
/// Azure SDK directly — that lives in Infrastructure.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file under the given logical folder (e.g. a product Id) and
    /// returns the publicly resolvable URL plus the storage-relative blob path.
    /// </summary>
    Task<(string Url, string BlobPath)> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the storage-relative blob path if the given URL points at a blob
    /// this service manages, or null if it's an externally-hosted URL (e.g. a
    /// CDN link registered via the legacy URL-only upload path) that must never
    /// be deleted by this service.
    /// </summary>
    string? TryGetManagedBlobPath(string url);
}
