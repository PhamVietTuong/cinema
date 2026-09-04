using System.Text;
using Cinema.Business;
using Cinema.Data;
using Cinema.Data.Contexts;
using Cinema.Data.Services;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Cinema.Service.WebApiHost.Hubs;
using Cinema.Service.WebApiHost.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using NSwag.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

FoundationLoggerFactory.SetupLogger(builder.Configuration);
builder.Services.AddFoundationLogging();

// Layers
builder.Services.AddBusiness();
builder.Services.AddData(builder.Configuration);

// Real-time seat-map broadcasts (SignalR) replace AddBusiness's no-op sender.
builder.Services.AddSingleton<Cinema.Business.Contracts.ISeatNotificationService, Cinema.Service.WebApiHost.Hubs.SignalRSeatNotificationService>();

// Real email delivery when SMTP is configured; otherwise AddBusiness's dev-log sender stays.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]))
{
    builder.Services.AddSingleton<Cinema.Business.Contracts.INotificationService, Cinema.Business.Notifications.SmtpNotificationService>();
}

// Real SMS delivery when Twilio is configured; otherwise AddBusiness's dev-log SMS sender stays.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Sms:Twilio:AccountSid"]))
{
    builder.Services.AddSingleton<Cinema.Business.Contracts.ISmsNotificationService, Cinema.Business.Notifications.TwilioSmsNotificationService>();
}

// Payment gateways. Sandbox is always available for dev; VNPay/MoMo/Stripe activate when their
// "Payments:*" config is filled in. "Payments:Provider" picks the default provider (Sandbox when unset).
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Cinema.Business.Contracts.Payments.IPaymentGateway, Cinema.Business.Payments.SandboxPaymentGateway>();
builder.Services.AddSingleton<Cinema.Business.Contracts.Payments.IPaymentGateway, Cinema.Business.Payments.VnPayGateway>();
builder.Services.AddSingleton<Cinema.Business.Contracts.Payments.IPaymentGateway, Cinema.Business.Payments.MoMoGateway>();
builder.Services.AddSingleton<Cinema.Business.Contracts.Payments.IPaymentGateway, Cinema.Business.Payments.StripeGateway>();
builder.Services.AddSingleton<Cinema.Business.Contracts.Payments.IPaymentGatewayResolver>(sp =>
    new Cinema.Business.Payments.PaymentGatewayResolver(
        sp.GetServices<Cinema.Business.Contracts.Payments.IPaymentGateway>(),
        builder.Configuration["Payments:Provider"]));

// Controllers
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR();

// Rate limiting for the unauthenticated identity endpoints
builder.Services.AddCinemaRateLimiting();

// Background job: expire abandoned unpaid bookings and free their seats
builder.Services.AddHostedService<Cinema.Service.WebApiHost.Services.PendingBookingReaper>();
// Background job: email showtime reminders ~1h before start
builder.Services.AddHostedService<Cinema.Service.WebApiHost.Services.ShowtimeReminderService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// JWT Authentication
var jwtSecret = builder.Configuration["JWT:Secret"];
// Fail fast if the signing key is missing or is the old committed default. Provide it out-of-band via
// user-secrets (dev) or the JWT__Secret environment variable (prod) — never commit it to source.
const string insecureDefaultJwtSecret = "CinemaSecretKey2024!SuperSecureAndLongEnoughForHS256Algorithm";
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret == insecureDefaultJwtSecret)
{
    throw new InvalidOperationException(
        "JWT:Secret is not configured, or is the insecure built-in default. Set a strong random value via " +
        "`dotnet user-secrets set \"JWT:Secret\" \"<value>\"` (Development) or the JWT__Secret environment variable.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JWT:Issuer"],
            ValidAudience = builder.Configuration["JWT:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Health checks — liveness at /health, readiness (incl. DB) at /health/ready
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CinemaContext>("database", tags: new[] { "ready" });

// NSwag — one OpenAPI document per controller group
static void ConfigureSecurity(NSwag.Generation.AspNetCore.AspNetCoreOpenApiDocumentGeneratorSettings cfg)
{
    cfg.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
    {
        Type        = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token"
    });
    cfg.OperationProcessors.Add(
        new NSwag.Generation.Processors.Security.OperationSecurityScopeProcessor("Bearer"));
}

builder.Services.AddOpenApiDocument(cfg =>
{
    cfg.DocumentName  = "cinema";
    cfg.Title         = "Cinema API";
    cfg.Version       = "v1";
    cfg.ApiGroupNames = new[] { "cinema" };
    ConfigureSecurity(cfg);
});

builder.Services.AddOpenApiDocument(cfg =>
{
    cfg.DocumentName  = "payment";
    cfg.Title         = "Payment API";
    cfg.Version       = "v1";
    cfg.ApiGroupNames = new[] { "payment" };
    ConfigureSecurity(cfg);
});

builder.Services.AddOpenApiDocument(cfg =>
{
    cfg.DocumentName  = "identity";
    cfg.Title         = "Identity API";
    cfg.Version       = "v1";
    cfg.ApiGroupNames = new[] { "identity" };
    ConfigureSecurity(cfg);
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CinemaContext>();
}

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/swagger";
        settings.SwaggerRoutes.Add(new NSwag.AspNetCore.SwaggerUiRoute("Cinema API",    "/swagger/cinema/swagger.json"));
        settings.SwaggerRoutes.Add(new NSwag.AspNetCore.SwaggerUiRoute("Payment API",   "/swagger/payment/swagger.json"));
        settings.SwaggerRoutes.Add(new NSwag.AspNetCore.SwaggerUiRoute("Identity API",  "/swagger/identity/swagger.json"));
    });
    app.UseReDoc(settings =>
    {
        settings.Path           = "/redoc";
        settings.DocumentPath   = "/swagger/cinema/swagger.json";
    });
}

app.UseCors("AllowFrontend");
app.UseRateLimiter();
// Serve uploaded images at /uploads/* from the on-disk uploads folder.
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionMiddleware>();
app.MapControllers();
app.MapHub<BookingHub>("/hubs/booking");
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
