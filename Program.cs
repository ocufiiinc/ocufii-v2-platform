using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using FluentValidation.AspNetCore;
using System.Text;
using OcufiiAPI.Configs;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using Microsoft.AspNetCore.Diagnostics;
using OcufiiAPI.Data;
using Microsoft.OpenApi.Models;
using OcufiiAPI.Handler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var projectRoot = Directory.GetCurrentDirectory();           // ← THIS IS PROJECT ROOT
var logPath = Path.Combine(projectRoot, "logs");

Directory.CreateDirectory(logPath);  // ← Creates logs folder in project root

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logPath, "ocufii-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        fileSizeLimitBytes: 50_000_000,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

// ==================== SERVICES ====================
builder.Services.AddControllers();

// FluentValidation – auto-discover all validators
builder.Services.AddFluentValidation(fv =>
{
    fv.RegisterValidatorsFromAssemblyContaining<Program>();
    fv.ImplicitlyValidateChildProperties = true;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Ocufii API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database
builder.Services.AddDbContext<OcufiiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OcufiiConnection"))
           .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddHttpContextAccessor();                                   // ← REQUIRED
builder.Services.AddScoped<IAuthorizationHandler, SameUserOrAdminHandler>(); // ← YOUR HANDLER

// Configs
builder.Services.Configure<JwtConfig>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<LegacyConfig>(builder.Configuration.GetSection("LegacyConfig"));

// JWT Authentication
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtConfig>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
        };
    });

builder.Services.Configure<SettingsDefaultsConfig>(builder.Configuration.GetSection("SettingsDefaults"));
builder.Services.Configure<AssistDefaultsConfig>(builder.Configuration.GetSection("AssistDefaults"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanEditOwnProfile", policy =>
        policy.AddRequirements(new SameUserOrAdminRequirement()));
});

var app = builder.Build();

// ==================== GLOBAL EXCEPTION HANDLER – CLEAN JSON ERRORS ====================
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var problem = new
        {
            type = "https://errors.ocufii.com/validation-error",
            title = "One or more validation errors occurred",
            status = 400,
            detail = exception?.Message,
            instance = context.Request.Path,
            errors = new Dictionary<string, string[]>()
        };

        if (exception is FluentValidation.ValidationException validationEx)
        {
            context.Response.StatusCode = 400;
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );
            problem = problem with
            {
                type = "https://errors.ocufii.com/validation-error",
                title = "Validation failed",
                errors = errors
            };
        }
        else
        {
            context.Response.StatusCode = 500;
            problem = problem with
            {
                type = "https://errors.ocufii.com/internal-error",
                title = "Internal server error",
                status = 500
            };
            Log.Error(exception, "Unhandled exception");
        }

        await context.Response.WriteAsJsonAsync(problem);
    });
});

// ==================== PIPELINE ====================
if (app.Environment.IsDevelopment() ||
    app.Environment.EnvironmentName.Equals("TestDevWeb", StringComparison.OrdinalIgnoreCase))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ocufii API v1");
        c.RoutePrefix = "swagger";
    });
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers.Append("X-Environment", app.Environment.EnvironmentName);
    }
    await next();
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===== AUTO RUN MIGRATIONS =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OcufiiDbContext>();
    db.Database.Migrate();
}
app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);
app.Run();