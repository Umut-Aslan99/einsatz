using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Einsatz.Api.Auth;
using Einsatz.Api.Data;
using Einsatz.Api.Demo;
using Einsatz.Api.Files;
using Einsatz.Api.GraphQL;
using Einsatz.Api.Integrations;
using Einsatz.Api.Jobs;
using Einsatz.Api.Office;
using Einsatz.Api.Reports;
using Einsatz.Api.Sync;
using Einsatz.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

services.AddDbContextFactory<AppDb>(o => o.UseNpgsql(config.GetConnectionString("Db")));
services.AddSingleton(TimeProvider.System);
services.AddHttpContextAccessor();
services.AddScoped<CurrentUser>();
services.AddScoped<JobReads>();
services.AddScoped<SyncService>();
services.AddScoped<ApprovalService>();
services.AddScoped<DemoSeeder>();
services.AddSingleton<ReportPdf>();
services.Configure<IntegrationOptions>(config.GetSection("Integrations"));
services.AddSingleton<IErpConnector, XmlErpConnector>();
services.AddSingleton<IDocumentArchive, FileSystemArchive>();
services.AddHostedService<NightlyReset>();

services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
services.AddProblemDetails();
services.AddOpenApi();

// Auth: HttpOnly SameSite=Strict cookie (no tokens in JS-readable storage).
services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(config["DataProtection:Path"] ?? "/data/keys"));
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "einsatz";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
services.AddAuthorizationBuilder()
    .AddPolicy("Technician", p => p.RequireRole(nameof(Role.Technician)))
    .AddPolicy("Office", p => p.RequireRole(nameof(Role.Office)));
services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

services.AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddErrorFilter(DomainErrors.Translate)
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = !builder.Environment.IsProduction());

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<JsonOnlyWrites>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/healthz", () => "ok").ExcludeFromDescription();
app.MapOpenApi("/api/openapi/{documentName}.json");
app.MapAuth();
app.MapSync();
app.MapFiles();
app.MapGraphQL("/graphql");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDb>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedIfEmptyAsync();
}

await app.RunAsync();

/// <summary>
/// CSRF defence in depth (on top of SameSite=Strict): state-changing requests must be JSON,
/// which a foreign site cannot send without a CORS preflight that this API never grants.
/// </summary>
internal sealed class JsonOnlyWrites(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext http)
    {
        var req = http.Request;
        if (HttpMethods.IsGet(req.Method) || HttpMethods.IsHead(req.Method) || HttpMethods.IsOptions(req.Method)) return next(http);
        if (req.ContentLength is 0 or null && !req.Headers.ContainsKey("Transfer-Encoding")) return next(http);
        if (req.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true
            || req.ContentType?.StartsWith("application/graphql-response+json", StringComparison.OrdinalIgnoreCase) == true) return next(http);
        http.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
        return Task.CompletedTask;
    }
}

public partial class Program;
