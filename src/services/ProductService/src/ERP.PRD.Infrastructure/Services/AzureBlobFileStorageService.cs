using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ERP.PRD.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace ERP.PRD.Infrastructure.Services;

/// <summary>
/// Stores product images in a single "product-images" Azure Blob Storage container
/// (public blob-level read access, so uploaded images are directly browser-renderable
/// without proxying through the API Gateway). Backed by Azurite locally via
/// ConnectionStrings:BlobStorage.
/// </summary>
public class AzureBlobFileStorageService : IFileStorageService
{
    private const string ContainerName = "product-images";

    private readonly BlobContainerClient _containerClient;
    private readonly string? _publicBaseUrl;
    private bool _containerEnsured;

    public AzureBlobFileStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);

        // ConnectionStrings:BlobStorage points at Azurite's Docker/k8s-internal hostname
        // (e.g. "azurite:10000"), which the SDK needs for server-to-server calls but which
        // the end user's browser can never resolve — it isn't on that internal network.
        // FileStorage:PublicBlobBaseUrl (e.g. "http://localhost:10000" for docker-compose,
        // or a NodePort URL for k8s) rewrites only the URL string handed back to API
        // clients; the SDK itself keeps using the internal endpoint. Left unset in real
        // Azure Storage deployments, where the connection string's endpoint already IS the
        // publicly resolvable one — no rewrite needed there.
        _publicBaseUrl = configuration["FileStorage:PublicBlobBaseUrl"]?.TrimEnd('/');
    }

    public async Task<(string Url, string BlobPath)> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);

        var extension = Path.GetExtension(fileName);
        var blobPath = string.IsNullOrEmpty(folder)
            ? $"{Guid.NewGuid():N}{extension}"
            : $"{folder}/{Guid.NewGuid():N}{extension}";

        var blobClient = _containerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(
            content,
            new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: cancellationToken);

        return (ToPublicUrl(blobClient.Uri), blobPath);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public string? TryGetManagedBlobPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var containerUri = _containerClient.Uri;
        var containerMarker = containerUri.AbsolutePath.TrimEnd('/') + "/";

        // A stored URL may carry either the internal host (uploaded before
        // PublicBlobBaseUrl was configured, or a same-host dev setup) or the public
        // base host (the normal case after upload) — recognize both as "ours".
        var matchesInternalHost =
            string.Equals(uri.Host, containerUri.Host, StringComparison.OrdinalIgnoreCase) &&
            uri.Port == containerUri.Port;
        var matchesPublicBase =
            !string.IsNullOrEmpty(_publicBaseUrl) &&
            url.StartsWith(_publicBaseUrl, StringComparison.OrdinalIgnoreCase);

        if (!matchesInternalHost && !matchesPublicBase)
            return null;

        if (!uri.AbsolutePath.StartsWith(containerMarker, StringComparison.OrdinalIgnoreCase))
            return null;

        return uri.AbsolutePath[containerMarker.Length..];
    }

    private string ToPublicUrl(Uri internalUri)
        => string.IsNullOrEmpty(_publicBaseUrl)
            ? internalUri.ToString()
            : $"{_publicBaseUrl}{internalUri.PathAndQuery}";

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
            return;

        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        _containerEnsured = true;
    }
}
