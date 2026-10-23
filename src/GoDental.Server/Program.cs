using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Security;
using GoDental.Server;
using GoDental.Server.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var listenUrl = builder.Configuration["Server:Url"] ?? "https://localhost:7443";
if (!Uri.TryCreate(listenUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
    uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
    throw new InvalidOperationException("Server:Url requiere una dirección HTTPS válida.");
builder.WebHost.UseUrls(listenUrl);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 8192;
    options.Limits.MaxRequestHeadersTotalSize = 8192;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
});
// Evitar logs automáticos de URL/query, cabeceras, cuerpos o excepciones con datos del proveedor.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Critical);
builder.Logging.AddFilter("Microsoft.Data.SqlClient", LogLevel.Critical);
builder.Services.AddOptions<ServerHostOptions>().BindConfiguration("Server")
    .Validate(options => WorkstationSettingsValidator.Validate(new(options.SqlServer, options.Database,
            LocalDevelopment: options.LocalDevelopmentSqlCertificate)) is null &&
        (!options.LocalDevelopmentSqlCertificate || builder.Environment.IsDevelopment()),
        "Configuración del servidor inválida; revisa SQL, TLS y límites locales.").ValidateOnStart();
builder.Services.AddSingleton<IDbConnectionFactory>(services =>
{
    var options = services.GetRequiredService<IOptions<ServerHostOptions>>().Value;
    return new ServerSqlConnectionFactory(new(options.SqlServer, options.Database,
        LocalDevelopment: options.LocalDevelopmentSqlCertificate), builder.Environment.IsDevelopment());
});
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IServerAuthenticationRepository, SqlServerAuthenticationRepository>();
builder.Services.AddScoped<IServerAuthenticationService, ServerAuthenticationService>();
builder.Services.AddScoped<ServerRequestContext>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.MaxDepth = 8;
});
builder.Services.AddAuthentication(OpaqueSessionHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, OpaqueSessionHandler>(OpaqueSessionHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options => options.AddPolicy("FiscalRead", policy =>
    policy.RequireAuthenticatedUser().RequireClaim("permission", "FISCAL.VER").RequireClaim("permission", "VENTAS.VER")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetConcurrencyLimiter(
            "host-work", _ => new() { PermitLimit = 4, QueueLimit = 0 })),
        PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetFixedWindowLimiter(
            "host-window", _ => new() { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new()
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
});

var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status503ServiceUnavailable;
    app.Logger.LogWarning("Solicitud del servidor interrumpida. Clase: {ErrorClass}", exception?.GetType().Name ?? "Unknown");
    context.Response.StatusCode = status;
    context.Response.Headers.CacheControl = "no-store";
    await context.Response.WriteAsJsonAsync(new { code = "REQUEST_UNAVAILABLE", message = "No se pudo completar la solicitud." });
}));
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { code = "HTTPS_REQUIRED" });
        return;
    }
    if (context.Request.ContentLength > 8192)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return;
    }
    await next();
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "RUNNING" })).AllowAnonymous();
app.MapPost("/api/session", async (ServerLoginRequest request, IServerAuthenticationService sessions, CancellationToken ct) =>
{
    var result = await sessions.SignInAsync(request, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.Json(new { code = result.ErrorCode, message = result.Message },
        statusCode: result.ErrorCode == "PASSWORD_CHANGE_REQUIRED" ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized);
}).AllowAnonymous().RequireRateLimiting("login");
app.MapGet("/api/session", (ServerRequestContext context) => Results.Ok(context.Operator)).RequireAuthorization();
app.MapDelete("/api/session", async (HttpContext context, IServerAuthenticationService sessions) =>
{
    await sessions.SignOutAsync(OpaqueSessionHandler.Token(context.Request)!, context.RequestAborted);
    return Results.NoContent();
}).RequireAuthorization();
app.MapGet("/api/fiscal/readiness", () => Results.Ok(new
{
    preparation = "INTERNAL_ONLY", generationEnabled = false, signingEnabled = false,
    transmissionEnabled = false, productionEnabled = false
})).RequireAuthorization("FiscalRead");

app.Run();

public partial class Program { }
