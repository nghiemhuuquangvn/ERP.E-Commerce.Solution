using Microsoft.AspNetCore.SignalR;
using ERP.CHB.API.Hubs;
using ERP.CHB.Application.Commands;
using ERP.CHB.Application.Interfaces;
using ERP.CHB.Application.Validators;
using ERP.CHB.Infrastructure;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Versioning;
using FluentValidation;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChatBotInfrastructure(builder.Configuration);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<SendMessageCommandHandler>();
});
builder.Services.AddValidatorsFromAssemblyContaining<SendMessageCommandValidator>();

builder.Services.AddScoped<IChatStreamNotifier, SignalRChatStreamNotifier>();

// Real JWT validation — see AuthenticationExtensions.AddErpJwtAuthentication /
// JwtClaimsUserContextService, same as every other service. The "/hubs" query-string
// fallback it adds is what lets ChatHub's WebSocket connections authenticate.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddErpAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
// Tenant isolation for ChatBotDbContext: JWT tenant, Gateway host tenant for anonymous REST
// calls (guest StartConversation), or the TenantScope HostTenantHubFilter sets for every
// /hubs/chat call (the hub bypasses the Gateway, so it resolves the host itself).
builder.Services.AddRequestTenantProvider();
builder.Services.AddTenantHostResolver(builder.Configuration);
builder.Services.AddScoped<UserContext>();
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.ChatBot));

// Backs IHubTicketStore (see AuthenticationExtensions' "/hubs" fallback and
// HubTicketController) — swaps a caller's large bearer token for a short single-use
// ticket so the SignalR connection URL doesn't blow past Kestrel's 8KB request-line limit.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IHubTicketStore, HubTicketStore>();

builder.Services.AddSignalR(o => o.AddFilter<ERP.CHB.API.Hubs.HostTenantHubFilter>());

builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ChatBot Service API", Version = "v1" });
});

// AllowCredentials is required for the SignalR browser client (cookies aren't used, but
// the WebSocket/SSE transports still need it) — so origins can't be "*" here; Storefront/
// Admin dev + prod origins are read from config instead of hardcoded, same shape as
// every other service's "AllowAll" policy would be too permissive for a credentialed hub.
//
// Also accepts any "*.erp.local" (or bare "erp.local") origin regardless of config —
// deployment/nginx/nginx.conf and this platform's local/K8s dev setups route through
// subdomains of erp.local (erp.local, shop.erp.local, api.erp.local, and whatever
// custom hosts a given environment adds for Admin/Storefront/the chat hub itself, e.g.
// system.erp.local / chat.erp.local) that can't be fully enumerated in one config list
// up front. Cors:AllowedOrigins remains for anything outside that pattern (e.g. plain
// localhost dev ports).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:5174" };
var allowedOriginSet = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);
builder.Services.AddCors(options =>
{
    options.AddPolicy("ChatBotCors", policy =>
        policy.SetIsOriginAllowed(origin =>
            {
                if (allowedOriginSet.Contains(origin))
                    return true;

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                return uri.Host.Equals("erp.local", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(".erp.local", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
builder.Services.AddHealthChecks();

var app = builder.Build();

// Was missing relative to every other service's Program.cs — without it, any unhandled
// exception (a bad request shape, a DB schema mismatch, a downstream 5xx bubbling up
// uncaught) returned a bare empty 500 with no ProblemDetails body, which is exactly what
// made a real bug here ("Conversation.IsDeleted" mapped by convention with no matching
// column in 001_CreateChatBotDB.sql) show up client-side as an unexplained
// "Request failed with status code 500" instead of a diagnosable error.
app.UseGlobalExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("ChatBotCors");
app.UseAuthentication();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
