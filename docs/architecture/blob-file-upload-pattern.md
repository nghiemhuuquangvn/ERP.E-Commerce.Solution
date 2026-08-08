# Blob File Upload Pattern (Azure Blob Storage / Azurite)

## When to reach for this

A service needs to accept real file uploads (not just URL strings pointing at
externally-hosted files) and serve them back to a browser. First implemented for
ProductService (`src/services/ProductService`) to let admins upload product image
files directly instead of only registering a URL they already host elsewhere
(`docs/business/Product/` — the admin "Add Product" image tab). No other service
has adopted this yet; the abstraction is intentionally scoped to ProductService's own
layers, not `src/Shared/`, since only one adopter exists so far (mirrors
`extensible-attributes-pattern.md`'s "don't share the table across services" stance —
here it's "don't share the abstraction until a second adopter actually needs it").

## Why Azure Blob Storage (with Azurite locally), not local disk

ProductService runs multiple replicas in both docker-compose and Kubernetes
(`deployment/k8s/deployments/product.yaml` sets `replicas: 2`). Storing uploads on
pod-local disk means whichever replica handled the upload is the only one that can
serve it back — a request routed to a different replica 404s. Blob storage sidesteps
this entirely: every replica talks to the same external store. Azurite (the official
Azure Storage emulator) stands in for a real Storage Account in local dev
(`docker-compose.yml`'s `azurite` service, `deployment/k8s/infrastructure/azurite.yaml`'s
StatefulSet) — same wire protocol, same SDK, zero code differences between dev and a
real Azure deployment.

## Layering (Clean Architecture)

```
ERP.PRD.Application/Interfaces/Services/IFileStorageService.cs   -- the abstraction
ERP.PRD.Infrastructure/Services/AzureBlobFileStorageService.cs   -- the only implementation
ERP.PRD.Application/Features/Images/Commands/
    UploadProductImageFileCommand.cs   -- multipart upload (new file)
    UploadProductImageCommand.cs       -- URL-registration (external/CDN images; kept side by side)
    UpdateProductImageCommand.cs       -- edit metadata, optionally replace the file
    RemoveProductImageCommand.cs       -- deletes the DB row + the blob, if we own it
ERP.PRD.API/Controllers/ImagesController.cs                      -- reads IFormFile, never leaks it past this layer
```

`IFileStorageService` takes a plain `Stream`/`byte[]`, never `Microsoft.AspNetCore.Http.IFormFile`
— the controller reads the file and passes a `Stream` + `FileName`/`ContentType`/`Length`
into the command, so Application stays framework-agnostic (no ASP.NET Core reference)
per this repo's layering rule. `Azure.Storage.Blobs` is referenced only from
Infrastructure, never Application.

```csharp
public interface IFileStorageService
{
    Task<(string Url, string BlobPath)> UploadAsync(
        Stream content, string fileName, string contentType, string folder, CancellationToken ct);
    Task DeleteAsync(string blobPath, CancellationToken ct);
    string? TryGetManagedBlobPath(string url); // null if this URL isn't one we own — see below
}
```

Container name is `product-images`, blob path convention is `{productId}/{guid}{extension}`,
created with `PublicAccessType.Blob` on first use (`AzureBlobFileStorageService.EnsureContainerAsync`)
so uploaded images are directly `<img src>`-renderable with no Gateway proxying.

## The internal-vs-public URL problem

`ConnectionStrings:BlobStorage` (e.g. `http://azurite:10000/...` in docker-compose,
or the in-cluster Service DNS name in k8s) is what the SDK needs for server-to-server
calls — and it is **not resolvable from the end user's browser**, which sits outside
that Docker/k8s-internal network entirely. Returning that URL straight to the client
renders as `net::ERR_NAME_NOT_RESOLVED`. `AzureBlobFileStorageService` fixes this by
rewriting *only the URL string handed back to API clients* through an optional
`FileStorage:PublicBlobBaseUrl` setting — the SDK itself keeps using the internal
connection string for every actual read/write:

| Environment | `ConnectionStrings:BlobStorage` host | `FileStorage:PublicBlobBaseUrl` |
|---|---|---|
| docker-compose | `azurite:10000` (container DNS) | `http://localhost:10000` (published port) |
| Kubernetes | in-cluster Service DNS | `http://localhost:30310` (new `azurite-nodeport` Service, `deployment/k8s/infrastructure/azurite.yaml`) |
| Real Azure Storage | the Storage Account's real hostname | unset — the connection string's endpoint is already public |

Because a stored image's URL might have been written under either form (e.g. before
`PublicBlobBaseUrl` was configured), `TryGetManagedBlobPath` — used by delete/replace
to decide whether a blob is safe to remove — recognizes a URL as "ours" if it matches
**either** the internal host+port **or** the configured public base, never touching a
URL that matches neither (an externally-hosted/CDN image registered via the plain
URL-registration command).

## Gotcha: EF Core tracks a new child entity as `Modified`, not `Added`

The single hardest bug hit building this (`git log`/PR history has the full
diagnostic trail). Symptom: `POST .../images/file` 500s with

```
DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s),
but actually affected 0 row(s)
```

immediately on a brand-new upload — no prior row could possibly be "concurrently
modified." Root cause, confirmed by logging `ex.Entries[0].State`: `ProductImage.Id`
is a **client-assigned Guid**, set in the entity's own constructor
(`Id = Guid.NewGuid()`) *before* EF ever sees it. When that entity is only reachable
via a tracked parent's collection navigation (`Product.AddImage(image)` doing
`_images.Add(image)`, not an explicit `DbSet.Add()`), EF Core's `DetectChanges` graph
walk applies the documented heuristic: a store-generated key that already has a
non-default value on a graph-discovered entity ⇒ state `Modified`, not `Added`.
SaveChanges then emits an `UPDATE ... WHERE Id = @p0` for a row that doesn't exist yet
— 0 rows affected, exception thrown before any error is logged (this makes it look
like a DB-level problem; it isn't — a raw SQL `INSERT` against the same table with the
same shape succeeds immediately, which is how this was isolated).

**Fix:** call the repository's own `AddAsync` explicitly, in addition to the domain
method that maintains the in-memory collection invariant:

```csharp
product.AddImage(image);                                  // in-memory collection + HasImage flag
await _uow.Images.AddAsync(image, cancellationToken);      // forces the correct EntityState.Added
await _uow.SaveChangesAsync(cancellationToken);
```

`DbSet.AddAsync()` unconditionally sets `EntityState.Added` regardless of any state
EF already inferred for that instance — safe to call even though the entity is
already graph-tracked. This is the general fix for *any* child entity in this
codebase that (a) has a client-assigned Guid key (the norm here — every `BaseEntity`
descendant does `Id = Guid.NewGuid()` in its constructor) **and** (b) is only ever
added via a parent aggregate's collection method, never through its own repository.
`ProductVariant`/`AddVariant` never hit this because `AddVariant` doesn't also mutate
a scalar property on `Product` in the same `SaveChanges` call — but the *class* of bug
(wrong state on a graph-discovered new child) applies regardless of whether a sibling
scalar update is involved; the safe default going forward is to always pair a
collection-navigation `Add` with an explicit repository `AddAsync` for any new
BaseEntity-derived child, not just when a symptom shows up.

## Gotcha: axios silently sends the wrong `Content-Type` for `FormData`

Frontend companion bug, same feature. `apiClient` (`src/frontend/admin/src/api/client.ts`)
sets `Content-Type: application/json` as an **instance-level default header**. Axios's
usual "detect `FormData`, let the browser set the multipart boundary" behavior does
not override a default configured this way (it reliably overrides a *per-request*
header, not an instance default) — the request goes out as JSON, ASP.NET Core's model
binder never sees a file part, and `[ApiController]`'s automatic validation 400s with
`"the file field is required"` *before* any application code runs (so nothing shows
up in the server logs — this looks like a routing problem, not a serialization one).
Fix, applied at each call site that posts `FormData` through `apiClient`
(`products.api.ts`'s `uploadImageFile`/`updateImage`):

```ts
apiClient.post(url, formData, { headers: { 'Content-Type': undefined } })
```

Explicitly unsetting the header (not omitting it) is what lets the browser supply the
correct `multipart/form-data; boundary=...` value itself.

## Reference implementation

- `src/services/ProductService/src/ERP.PRD.Application/Interfaces/Services/IFileStorageService.cs`
- `src/services/ProductService/src/ERP.PRD.Infrastructure/Services/AzureBlobFileStorageService.cs`
- `src/services/ProductService/src/ERP.PRD.Application/Features/Images/Commands/` — all four commands listed above
- `src/services/ProductService/src/ERP.PRD.Application/Interfaces/IProductRepositories.cs` — `IProductImageRepository.AddAsync`
- `src/services/ProductService/src/ERP.PRD.API/Controllers/ImagesController.cs`
- `src/frontend/admin/src/api/products.api.ts` — `uploadImageFile`/`updateImage`
- `src/frontend/admin/src/pages/products/ProductImagesTab.tsx` — drag-and-drop UI, "Replace" action
- `docker-compose.yml` (`azurite` service + `productservice`'s `FileStorage__PublicBlobBaseUrl`)
- `deployment/k8s/infrastructure/azurite.yaml`, `deployment/k8s/deployments/product.yaml`

## Related patterns in this codebase

- `extensible-attributes-pattern.md` — the other significant ProductService-first
  pattern from the same effort; same "one adopter so far, not yet promoted to Shared"
  posture.
- `docs/deployment/rebuild-and-redeploy-guide.md` §5 — the Gateway-rebuild pitfall
  hit while deploying this feature (a new Ocelot route needs the Gateway itself
  rebuilt, not just the downstream service).
