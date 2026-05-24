using System.Text;
using Cinema.Business;
using Cinema.Data;
using Cinema.Data.Contexts;
using Cinema.Data.Services;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Hubs;
using Cinema.Service.WebApiHost.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NSwag.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFoundationLogging();

// Layers
builder.Services.AddBusiness();
builder.Services.AddData(builder.Configuration);

// Controllers
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR();

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
var jwtSecret = builder.Configuration["JWT:Secret"]!;
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
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionMiddleware>();
app.MapControllers();
app.MapHub<BookingHub>("/hubs/booking");

app.Run();
