using System.Text;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Configuration;
using FluentValidation;
using FluentValidation.AspNetCore;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.RepairOrders;
using RepairShop.Application.Security;
using RepairShop.Infrastructure;
using RepairShop.Infrastructure.Persistence;
using Serilog;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var isSeedCommand = args.Any(a => string.Equals(a, "--seed", StringComparison.OrdinalIgnoreCase))
    || (args.Length >= 2
        && string.Equals(args[0], "db", StringComparison.OrdinalIgnoreCase)
        && string.Equals(args[1], "seed", StringComparison.OrdinalIgnoreCase));

// Optional local overrides (gitignored). Useful for dev without leaking secrets into the repo.
// NOTE: we re-add env vars + command-line after this so they keep highest precedence.
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Serilog (structured logging)
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddMemoryCache();

// Reverse proxy support (Nginx/Traefik/Cloudflare): honor X-Forwarded-* so
// scheme + client IP are correct (important for HSTS + login throttling).
// NOTE: For strict production, prefer restricting KnownProxies/KnownNetworks.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Operational security: login rate-limit + lockout (in-memory basic)
builder.Services.Configure<LoginSecurityOptions>(builder.Configuration.GetSection(LoginSecurityOptions.SectionName));
builder.Services.AddSingleton<LoginSecurityService>();

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Standardize invalid-model responses into ValidationProblemDetails
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ctx =>
    {
        var problem = new ValidationProblemDetails(ctx.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation error",
            Type = "https://httpstatuses.com/400",
            Instance = ctx.HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        var corr = CorrelationIdMiddleware.TryGet(ctx.HttpContext);
        if (!string.IsNullOrWhiteSpace(corr)) problem.Extensions["correlationId"] = corr;

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddDbContextCheck<RepairShopDbContext>("db", tags: new[] { "ready" });

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Contains("__SET_VIA_ENV_OR_SECRETS__", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "JWT signing key is not configured. Set Jwt__Key via environment variables / secrets, " +
        "or provide it in appsettings.Local.json (gitignored) for development.");
}
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
    // "Staff" in docs = technicians/operators. We support both role names to avoid confusion.
    options.AddPolicy(Policies.StaffOnly, p => p.RequireRole(Roles.Admin, Roles.Tech, Roles.Staff));
    options.AddPolicy(Policies.TechOnly, p => p.RequireRole(Roles.Tech, Roles.Staff));
});

// CORS (by environment)
builder.Services.AddCors(options =>
{
    options.AddPolicy(Policies.CorsDefault, policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        if (builder.Environment.IsDevelopment())
        {
            // Dev defaults (can be overridden via Cors:AllowedOrigins)
            if (origins.Length == 0)
            {
                origins = new[]
                {
                    "http://localhost:5173",
                    "http://localhost:8080"
                };
            }

            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            if (!isSeedCommand && origins.Length == 0)
            {
                throw new InvalidOperationException(
                    "CORS is not configured for non-development environments. " +
                    "Set Cors__AllowedOrigins__0 (and more) via env vars / secrets (prod/staging)."
                );
            }

            if (!isSeedCommand && origins.Any(o => o.Trim() == "*"))
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins cannot contain '*'. In production, explicitly list allowed origins."
                );
            }

            if (origins.Length > 0)
            {
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        }
    });
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RepairShop API", Version = "v1" });

    // XML docs (if present)
    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.OperationFilter<RepairShop.Api.Swagger.CorrelationIdHeaderOperationFilter>();
    c.OperationFilter<RepairShop.Api.Swagger.DefaultProblemDetailsResponsesOperationFilter>();

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste: Bearer {token}"
    };

    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { scheme, new List<string>() }
    });
});

var cs = builder.Configuration.GetConnectionString("RepairShopDb");
if (string.IsNullOrWhiteSpace(cs) || cs.Contains("__CHANGE_ME__", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Database connection string is not configured. Set ConnectionStrings__RepairShopDb via environment variables / secrets, " +
        "or provide it in appsettings.Local.json (gitignored) for development.");
}

builder.Services.AddInfrastructure(builder.Configuration);

// Optional OpenTelemetry (toggle via OpenTelemetry:Enabled)
if (builder.Configuration.GetValue<bool>("OpenTelemetry:Enabled"))
{
    var resource = ResourceBuilder.CreateDefault().AddService("RepairShop.Api");
    var otlp = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint");

    builder.Services.AddOpenTelemetry()
        .WithTracing(t =>
        {
            t.SetResourceBuilder(resource);
            t.AddAspNetCoreInstrumentation();
            t.AddHttpClientInstrumentation();

            if (!string.IsNullOrWhiteSpace(otlp))
                t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            else
                t.AddOtlpExporter();
        })
        .WithMetrics(m =>
        {
            m.SetResourceBuilder(resource);
            m.AddAspNetCoreInstrumentation();
            m.AddHttpClientInstrumentation();
            m.AddRuntimeInstrumentation();

            if (!string.IsNullOrWhiteSpace(otlp))
                m.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            else
                m.AddOtlpExporter();
        });
}

// Application services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RenderOrderMessageService>();
builder.Services.AddScoped<ChangeOrderStatusService>();

// Api services
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddTransient<ProblemDetailsMiddleware>();
builder.Services.AddTransient<CorrelationIdMiddleware>();

var app = builder.Build();

// Must be early so subsequent middleware sees correct scheme + client IP.
// Important behind reverse proxies (Cloudflare/Nginx/Traefik).
app.UseForwardedHeaders();

// Correlation id must come early so everything (including errors) gets it.
app.UseMiddleware<CorrelationIdMiddleware>();

// Structured request logging
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        var corr = CorrelationIdMiddleware.TryGet(http);
        if (!string.IsNullOrWhiteSpace(corr)) diag.Set("CorrelationId", corr);
        diag.Set("TraceId", http.TraceIdentifier);
    };
});

// Hard rules for production.
if (app.Environment.IsProduction() && !isSeedCommand)
{
    if (builder.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        throw new InvalidOperationException("Swagger:Enabled cannot be true in Production.");
    }

    if (builder.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        throw new InvalidOperationException("Seed:Enabled cannot be true in Production. Use the explicit seed command instead.");
    }

    var allowedHosts = (builder.Configuration["AllowedHosts"] ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(allowedHosts))
    {
        throw new InvalidOperationException(
            "AllowedHosts is required in Production. Set AllowedHosts to your API domain(s) (e.g. api.example.com)."
        );
    }

    if (string.Equals(allowedHosts, "*", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "AllowedHosts='*' is not allowed in Production. Set AllowedHosts to your domain(s) (e.g. api.example.com)."
        );
    }
}

var swaggerEnabled = app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HSTS only makes sense for HTTPS. With reverse proxies, UseForwardedHeaders
// ensures X-Forwarded-Proto is honored.
if (app.Environment.IsProduction())
{
    app.UseHsts();
}

// In containers / behind a reverse-proxy we usually terminate TLS upstream.
// Enable this only when the app is actually listening on HTTPS.
if (builder.Configuration.GetValue<bool>("HttpsRedirection:Enabled"))
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<ProblemDetailsMiddleware>();

app.UseCors(Policies.CorsDefault);

app.UseAuthentication();
app.UseAuthorization();

// Workflow orders are mutated through the transactional v2 commands.
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api/v1/orders") && !HttpMethods.IsGet(context.Request.Method)) {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { detail = "Usá los comandos de RepairShop v2 para modificar órdenes." });
        return;
    }
    await next();
});

app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api/v1/inventory") && !HttpMethods.IsGet(context.Request.Method)) {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { detail = "Usá Stock avanzado para mantener lotes, reservas y sucursales sincronizados." });
        return;
    }
    await next();
});

app.MapControllers();

// /healthz = liveness (no dependencies)
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteJson
});

// /readyz = readiness (DB check)
app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJson
});

await InitializeDatabaseAsync(app, builder.Configuration, forceSeed: isSeedCommand);

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("LocalDemo:Enabled")) {
    using var localScope = app.Services.CreateScope();
    await RepairShop.Api.V2.LocalDemoSeeder.Seed(localScope.ServiceProvider.GetRequiredService<RepairShopDbContext>(), localScope.ServiceProvider.GetRequiredService<RepairShop.Application.Security.IPasswordHasher>());
    await RepairShop.Api.V2.PremiumSeeder.Seed(localScope.ServiceProvider.GetRequiredService<RepairShopDbContext>());
}

if (isSeedCommand)
{
    // One-shot seed mode (e.g. docker compose run api --seed)
    return;
}

app.Run();

static async Task InitializeDatabaseAsync(
    WebApplication app,
    IConfiguration config,
    bool forceSeed,
    CancellationToken ct = default)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();

    var initMode = (config.GetValue<string>("Database:Init") ?? "Migrate").Trim();
    var retries = config.GetValue<int?>("Database:ConnectRetries") ?? 30;
    var delayMs = config.GetValue<int?>("Database:ConnectDelayMs") ?? 2000;

    if (string.Equals(initMode, "None", StringComparison.OrdinalIgnoreCase))
    {
        app.Logger.LogInformation("Database init disabled (Database:Init=None).");
        return;
    }

    for (var attempt = 1; attempt <= retries; attempt++)
    {
        try
        {
            if (string.Equals(initMode, "EnsureCreated", StringComparison.OrdinalIgnoreCase))
            {
                await db.Database.EnsureCreatedAsync(ct);
                app.Logger.LogInformation("Database EnsureCreated completed.");
            }
            else
            {
					// EF Core exposes GetMigrations() (sync). There's no GetMigrationsAsync on DatabaseFacade.
					var hasAnyMigrations = db.Database.GetMigrations().Any();
					if (!hasAnyMigrations)
					{
						var allowFallback =
							app.Environment.IsDevelopment() &&
							(config.GetValue<bool?>("Database:AllowEnsureCreatedFallback") ?? true);

						if (!allowFallback)
						{
							throw new InvalidOperationException(
								"Database:Init=Migrate but no EF Core migrations were found. " +
								"Generate migrations (dotnet ef migrations add ...) or set Database:Init=EnsureCreated (DEV only)."
							);
						}

						app.Logger.LogWarning(
							"No EF Core migrations were found. Using EnsureCreated() because we're in Development. " +
							"For production, generate real migrations and keep Database:Init=Migrate.");
						await db.Database.EnsureCreatedAsync(ct);
						app.Logger.LogInformation("Database EnsureCreated completed (dev fallback).");
					}
					else
					{
						await db.Database.MigrateAsync(ct);
						app.Logger.LogInformation("Database migrations applied.");
					}
            }

            break;
        }
        catch (Exception ex) when (attempt < retries)
        {
            app.Logger.LogWarning(ex,
                "Database init failed (attempt {Attempt}/{Retries}). Retrying in {Delay}ms...",
                attempt, retries, delayMs);

            await Task.Delay(delayMs, ct);
        }
    }

    // Seed rules:
    // - Development: enabled by default (can be turned off via Seed:Enabled=false)
    // - Non-Development: ONLY via explicit command (--seed or "db seed")
    var seedEnabled = forceSeed
        || (app.Environment.IsDevelopment() && (config.GetValue<bool?>("Seed:Enabled") ?? true));

    if (seedEnabled)
    {
        using var seedScope = app.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<RepairShopDbContext>();

        var clock = seedScope.ServiceProvider.GetRequiredService<RepairShop.Application.Abstractions.IDateTimeProvider>();
        var hasher = seedScope.ServiceProvider.GetRequiredService<RepairShop.Application.Security.IPasswordHasher>();
        var shopName = config.GetValue<string>("Seed:ShopName") ?? "TechXto";

		try
		{
			await DbSeeder.SeedAsync(seedDb, clock, hasher, shopName, ct);
			app.Logger.LogInformation("Database seed completed.");
		}
		catch (Exception seedEx)
		{
			// Never crash the container because seed failed.
			app.Logger.LogError(seedEx, "Database seed failed.");
		}
    }
}
