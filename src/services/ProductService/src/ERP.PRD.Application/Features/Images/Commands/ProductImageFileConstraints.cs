namespace ERP.PRD.Application.Features.Images.Commands
{
    /// <summary>
    /// Shared upload constraints for the file-based image commands, mirroring the
    /// hardcoded-constant style already used by UploadProductImageValidator's URL rules
    /// rather than introducing a configuration-binding dependency into this layer.
    /// </summary>
    public static class ProductImageFileConstraints
    {
        public const long MaxSizeBytes = 5 * 1024 * 1024;

        public static readonly string[] AllowedContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
        };
    }
}
