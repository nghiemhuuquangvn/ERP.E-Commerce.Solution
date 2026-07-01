using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ERP.Shared.Localization
{
    public static class LocalizationApplicationBuilderExtensions
    {
        /// <summary>
        /// Applies the RequestLocalizationOptions configured by AddSharedLocalization.
        /// Call after building the app, before request-handling middleware that reads
        /// CultureInfo.CurrentUICulture (e.g. before UseMiddleware&lt;UserContextHeaderMiddleware&gt;()).
        /// </summary>
        public static IApplicationBuilder UseSharedLocalization(this IApplicationBuilder app)
        {
            var options = app.ApplicationServices.GetRequiredService<IOptions<RequestLocalizationOptions>>();
            return app.UseRequestLocalization(options.Value);
        }
    }
}
