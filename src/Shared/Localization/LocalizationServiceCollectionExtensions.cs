using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Localization
{
    /// <summary>
    /// Registers request-culture negotiation shared by every service. Each service adds
    /// its own "Localization:SupportedCultures"/"Localization:DefaultCulture" config
    /// (falls back to en-US/vi-VN when absent).
    /// </summary>
    public static class LocalizationServiceCollectionExtensions
    {
        private static readonly string[] DefaultSupportedCultures = { "en-US", "vi-VN" };

        public static IServiceCollection AddSharedLocalization(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddLocalization(options => options.ResourcesPath = "Resources");

            var supportedCultures = configuration.GetSection("Localization:SupportedCultures").Get<string[]>()
                ?? DefaultSupportedCultures;
            var defaultCulture = configuration["Localization:DefaultCulture"] ?? supportedCultures.FirstOrDefault() ?? "en-US";

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var cultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();

                options.DefaultRequestCulture = new RequestCulture(defaultCulture);
                options.SupportedCultures = cultures;
                options.SupportedUICultures = cultures;
                options.RequestCultureProviders = new IRequestCultureProvider[]
                {
                    new QueryStringRequestCultureProvider { QueryStringKey = "culture", UIQueryStringKey = "culture" },
                    new AcceptLanguageHeaderRequestCultureProvider()
                };
            });

            return services;
        }
    }
}
