using Confluent.Kafka;
using ERP.MST.Application.EventHandlers;
using ERP.MST.Application.Interfaces;
using ERP.MST.Infrastructure.BackgroundJobs;
using ERP.MST.Infrastructure.MessageBus;
using ERP.MST.Infrastructure.Persistence;
using ERP.MST.Infrastructure.Repositories;
using ERP.MST.Infrastructure.Services;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.MST.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MasterDataDb");
            services.AddDbContext<MasterDataDbContext>(options =>
                options.UseSqlServer(connectionString));

            var kafkaServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = kafkaServers,
                AllowAutoCreateTopics = true
            };

            services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
            services.AddScoped<IMessageBus, KafkaMessageBus>();

            services.AddHostedService<OutboxProcessor>();
            services.AddHostedService<HrmEventConsumer>();
            services.AddHostedService<IdentityEventConsumer>();

            services.AddScoped<UserCreatedEventHandler>();
            services.AddScoped<LeaveApprovedEventHandler>();

            services.AddCacheService(configuration, "mst");

            // Repositories
            services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            services.AddScoped<IPositionRepository, PositionRepository>();
            services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            services.AddScoped<IUnitOfMeasureRepository, UnitOfMeasureRepository>();
            services.AddScoped<ICountryRepository, CountryRepository>();
            services.AddScoped<IProvinceRepository, ProvinceRepository>();
            services.AddScoped<IDistrictRepository, DistrictRepository>();
            services.AddScoped<IWardRepository, WardRepository>();
            services.AddScoped<ISupportedLanguageRepository, SupportedLanguageRepository>();
            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<IBranchRepository, BranchRepository>();
            services.AddScoped<IDivisionRepository, DivisionRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();
            services.AddScoped<IBusinessUnitRepository, BusinessUnitRepository>();
            services.AddScoped<ICostCenterRepository, CostCenterRepository>();
            services.AddScoped<IProfitCenterRepository, ProfitCenterRepository>();
            services.AddScoped<IOrganizationCalendarRepository, OrganizationCalendarRepository>();
            services.AddScoped<IHolidayRepository, HolidayRepository>();
            services.AddScoped<IEmployeeTeamAssignmentRepository, EmployeeTeamAssignmentRepository>();
            services.AddScoped<ICurrencyRepository, CurrencyRepository>();
            services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
            services.AddScoped<IPaymentTermRepository, PaymentTermRepository>();

            // Currency Settings (System/Tenant two-tier) — storage+CRUD only for now
            services.AddScoped<ICurrencySettingsService, CurrencySettingsService>();

            return services;
        }
    }
}
