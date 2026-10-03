using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Api.Validation;
using RepairShop.Application;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Security;
using RepairShop.Infrastructure;
using RepairShop.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var isSeedCommand = args.Any(a => string.Equals(a, "--seed", StringComparison.OrdinalIgnoreCase))
    || (args.Length >= 2
        && string.Equals(args[0], "db", StringComparison.OrdinalIgnoreCase)
        && string.Equals(args[1], "seed", StringComparison.OrdinalIgnoreCase));

// Optional local overrides (gitignored). Env vars + command line are re-added so they keep precedence.
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var config = builder.Configuration;

builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId();
});

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 20 * 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 20 * 1024 * 1024);

// ===== MVC, JSON and validation (Spanish messages) =====
builder.Services
    .AddControllers(options =>
    {
        var m = options.ModelBindingMessageProvider;
        m.SetAttemptedValueIsInvalidAccessor((value, field) => $"El valor '{value}' no es válido para {field}.");
        m.SetMissingBindRequiredValueAccessor(field => $"Falta el valor de {field}.");
        m.SetMissingKeyOrValueAccessor(() => "Falta un valor.");
        m.SetMissingRequestBodyRequiredValueAccessor(() => "Falta el cuerpo de la solicitud.");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"El valor '{value}' no es válido.");
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor no es válido.");
        m.SetNonPropertyValueMustBeANumberAccessor(() => "El valor debe ser un número.");
        m.SetUnknownValueIsInvalidAccessor(field => $"El valor de {field} no es válido.");
        m.SetValueIsInvalidAccessor(value => $"El valor '{value}' no es válido.");
        m.SetValueMustBeANumberAccessor(field => $"{field} debe ser un número.");
        m.SetValueMustNotBeNullAccessor(_ => "El valor es obligatorio.");
    })
    .AddJsonOptions(o =>
    {
        // Enums travel as names ("InProgress", "Cash"); numbers are still accepted on input.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

ValidatorOptions.Global.LanguageManager = new SpanishLanguageManager();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kv => ValidationText.FieldKey(kv.Key),
                kv => kv.Value!.Errors.Select(e => ValidationText.Translate(e.ErrorMessage, kv.Key, e.Exception)).Distinct().ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Revisá los datos ingresados",
            Detail = errors.Values.SelectMany(v => v).FirstOrDefault(),
            Type = "https://httpstatuses.com/400",
            Instance = ctx.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        var corr = CorrelationIdMiddleware.TryGet(ctx.HttpContext);
        if (!string.IsNullOrWhiteSpace(corr)) problem.Extensions["correlationId"] = corr;

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});

// ===== Reverse proxy: only trust X-Forwarded-* from known proxies (prevents IP spoofing of rate limits) =====
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = config.GetValue("ReverseProxy:ForwardLimit", 1);

    var proxies = config.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>();
    var networks = config.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>();
    foreach (var p in proxies.Where(p => !string.IsNullOrWhiteSpace(p)))
        options.KnownProxies.Add(IPAddress.Parse(p.Trim()));
    foreach (var n in networks.Where(n => !string.IsNullOrWhiteSpace(n)))
    {
        var parts = n.Trim().Split('/');
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(parts[0]), parts.Length > 1 ? int.Parse(parts[1]) : 32));
    }

    var allowedHosts = config.GetSection("ReverseProxy:AllowedHosts").Get<string[]>();
    if (allowedHosts is { Length: > 0 }) options.AllowedHosts = allowedHosts;
});

builder.Services.AddRateLimiter(o => RateLimits.Configure(o, config));

builder.Services.Configure<LoginSecurityOptions>(config.GetSection(LoginSecurityOptions.SectionName));
builder.Services.AddSingleton<LoginSecurityService>();

// ===== Health checks =====
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddDbContextCheck<RepairShopDbContext>("db", tags: new[] { "ready" });

// ===== Authentication (JWT access token + security stamp check) and authorization =====
builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Contains("__SET_VIA_ENV_OR_SECRETS__", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "JWT signing key is not configured. Set Jwt__Key via environment variables / secrets, " +
        "or provide it in appsettings.Local.json (gitignored) for development.");
}
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimNames.Name,
            RoleClaimType = ClaimNames.Role,
        };
        options.Events = new JwtBearerEvents { OnTokenValidated = SecurityStampValidator.ValidateAsync };
    });
builder.Services.AddAuthorization(Policies.Register);

// ===== CORS (credentials are needed for the refresh cookie) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy(Policies.CorsDefault, policy =>
    {
        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (builder.Environment.IsDevelopment() && origins.Length == 0)
            origins = new[] { "http://localhost:5173", "http://localhost:8080", "http://127.0.0.1:5173" };

        if (!builder.Environment.IsDevelopment() && !isSeedCommand)
        {
            if (origins.Length == 0)
                throw new InvalidOperationException("CORS is not configured. Set Cors__AllowedOrigins__0 (and more) via env vars / secrets.");
            if (origins.Any(o => o.Trim() == "*"))
                throw new InvalidOperationException("Cors:AllowedOrigins cannot contain '*'. List the allowed origins explicitly.");
        }

        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders(HttpExtensions.TotalCountHeader, CorrelationIdMiddleware.HeaderName, "Content-Disposition", "Retry-After", IdempotentAttribute.ReplayHeader)
                .SetPreflightMaxAge(TimeSpan.FromHours(1));
        }
    });
});

// ===== Swagger =====
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RepairShop API", Version = "v1" });
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
    c.OperationFilter<RepairShop.Api.Swagger.CorrelationIdHeaderOperationFilter>();
    c.OperationFilter<RepairShop.Api.Swagger.DefaultProblemDetailsResponsesOperationFilter>();

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Access token de /api/v1/auth/login",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, new List<string>() } });
});

// ===== Database + layers =====
var cs = config.GetConnectionString("RepairShopDb");
if (string.IsNullOrWhiteSpace(cs) || cs.Contains("__CHANGE_ME__", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Database connection string is not configured. Set ConnectionStrings__RepairShopDb via environment variables / secrets, " +
        "or provide it in appsettings.Local.json (gitignored) for development.");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddInfrastructure(config, enableBackgroundWork: !isSeedCommand && config.GetValue("BackgroundJobs:Enabled", true));
builder.Services.AddApplication();

builder.Services.Configure<AppOptions>(config.GetSection(AppOptions.SectionName));
builder.Services.Configure<RefreshCookieOptions>(config.GetSection(RefreshCookieOptions.SectionName));
builder.Services.AddSingleton<IAppLinks, AppLinks>();
builder.Services.AddSingleton<IFileUrlSigner, FileUrlSigner>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<RefreshCookie>();
builder.Services.AddTransient<ProblemDetailsMiddleware>();
builder.Services.AddTransient<CorrelationIdMiddleware>();

// ===== Optional OpenTelemetry =====
if (config.GetValue<bool>("OpenTelemetry:Enabled"))
{
    var resource = ResourceBuilder.CreateDefault().AddService("RepairShop.Api");
    var otlp = config.GetValue<string>("OpenTelemetry:OtlpEndpoint");

    builder.Services.AddOpenTelemetry()
        .WithTracing(t =>
        {
            t.SetResourceBuilder(resource).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
            if (!string.IsNullOrWhiteSpace(otlp)) t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            else t.AddOtlpExporter();
        })
        .WithMetrics(m =>
        {
            m.SetResourceBuilder(resource).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation();
            if (!string.IsNullOrWhiteSpace(otlp)) m.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            else m.AddOtlpExporter();
        });
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        var corr = CorrelationIdMiddleware.TryGet(http);
        if (!string.IsNullOrWhiteSpace(corr)) diag.Set("CorrelationId", corr);
        diag.Set("TraceId", http.TraceIdentifier);
    };
});

if (app.Environment.IsProduction() && !isSeedCommand)
{
    if (config.GetValue<bool>("Swagger:Enabled"))
        throw new InvalidOperationException("Swagger:Enabled cannot be true in Production.");
    if (config.GetValue<bool>("Seed:Enabled"))
        throw new InvalidOperationException("Seed:Enabled cannot be true in Production. Use the explicit seed command instead.");

    var allowedHosts = (config["AllowedHosts"] ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
        throw new InvalidOperationException("AllowedHosts must list your API domain(s) in Production (e.g. api.example.com).");
}

if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsProduction()) app.UseHsts();
if (config.GetValue<bool>("HttpsRedirection:Enabled")) app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseCors(Policies.CorsDefault);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteJson
}).AllowAnonymous();

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJson
}).AllowAnonymous();

await InitializeDatabaseAsync(app, config, forceSeed: isSeedCommand);

if (isSeedCommand) return;

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app, IConfiguration config, bool forceSeed, CancellationToken ct = default)
{
    var initMode = (config.GetValue<string>("Database:Init") ?? "Migrate").Trim();
    var retries = config.GetValue<int?>("Database:ConnectRetries") ?? 30;
    var delayMs = config.GetValue<int?>("Database:ConnectDelayMs") ?? 2000;

    if (string.Equals(initMode, "None", StringComparison.OrdinalIgnoreCase))
    {
        app.Logger.LogInformation("Database init disabled (Database:Init=None).");
    }
    else
    {
        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();
                if (string.Equals(initMode, "EnsureCreated", StringComparison.OrdinalIgnoreCase))
                {
                    await db.Database.EnsureCreatedAsync(ct);
                    app.Logger.LogInformation("Database EnsureCreated completed.");
                }
                else
                {
                    await db.Database.MigrateAsync(ct);
                    app.Logger.LogInformation("Database migrations applied.");
                }
                break;
            }
            catch (Exception ex) when (attempt < retries)
            {
                app.Logger.LogWarning(ex, "Database init failed (attempt {Attempt}/{Retries}). Retrying in {Delay}ms...", attempt, retries, delayMs);
                await Task.Delay(delayMs, ct);
            }
        }
    }

    // Development: seeded by default (Seed:Enabled=false to skip). Other environments: only with --seed / "db seed".
    var seedEnabled = forceSeed || (app.Environment.IsDevelopment() && (config.GetValue<bool?>("Seed:Enabled") ?? true));
    if (!seedEnabled) return;

    using var seedScope = app.Services.CreateScope();
    var sp = seedScope.ServiceProvider;
    try
    {
        await DbSeeder.SeedAsync(
            sp.GetRequiredService<RepairShopDbContext>(),
            sp.GetRequiredService<IDateTimeProvider>(),
            sp.GetRequiredService<IPasswordHasher>(),
            config.GetValue<string>("Seed:ShopName") ?? "TechXto",
            demoData: config.GetValue("Seed:DemoData", app.Environment.IsDevelopment()),
            ct);
        app.Logger.LogInformation("Database seed completed.");
    }
    catch (Exception seedEx)
    {
        // Never crash the container because the seed failed.
        app.Logger.LogError(seedEx, "Database seed failed.");
    }
}

/// <summary>Translates the framework's English validation messages and normalizes field keys to camelCase.</summary>
internal static class ValidationText
{
    private static readonly Regex Required = new(@"^The (?<f>.+?) field is required\.$", RegexOptions.Compiled);
    private static readonly Regex MinLength = new(@"^The field (?<f>.+?) must be a string or array type with a minimum length of '(?<n>\d+)'\.$", RegexOptions.Compiled);
    private static readonly Regex MaxLength = new(@"^The field (?<f>.+?) must be a string or array type with a maximum length of '(?<n>\d+)'\.$", RegexOptions.Compiled);
    private static readonly Regex Range = new(@"^The field (?<f>.+?) must be between (?<a>.+?) and (?<b>.+?)\.$", RegexOptions.Compiled);

    public static string FieldKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        if (key.StartsWith("$.", StringComparison.Ordinal)) key = key[2..];
        else if (key == "$") return "body";
        return string.Join('.', key.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
    }

    public static string Translate(string message, string key, Exception? exception)
    {
        if (string.IsNullOrWhiteSpace(message))
            return exception is not null ? $"El campo {FieldKey(key)} tiene un formato inválido." : "Valor inválido.";

        if (message.StartsWith("The JSON value could not be converted", StringComparison.Ordinal) ||
            message.StartsWith("'", StringComparison.Ordinal) && message.Contains("is an invalid", StringComparison.Ordinal))
            return $"El campo {FieldKey(key)} tiene un formato inválido.";

        var m = Required.Match(message);
        if (m.Success) return m.Groups["f"].Value is "body" or "req" ? "Falta el cuerpo de la solicitud." : $"El campo {FieldKey(m.Groups["f"].Value)} es obligatorio.";
        m = MinLength.Match(message);
        if (m.Success) return $"El campo {FieldKey(m.Groups["f"].Value)} debe tener al menos {m.Groups["n"].Value} caracteres.";
        m = MaxLength.Match(message);
        if (m.Success) return $"El campo {FieldKey(m.Groups["f"].Value)} no puede superar los {m.Groups["n"].Value} caracteres.";
        m = Range.Match(message);
        if (m.Success) return $"El campo {FieldKey(m.Groups["f"].Value)} debe estar entre {m.Groups["a"].Value} y {m.Groups["b"].Value}.";

        return message;
    }
}

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program
{
}
