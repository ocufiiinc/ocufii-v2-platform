using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OcufiiAPI.Configs;
using OcufiiAPI.Data;
using OcufiiAPI.Repositories;
using OcufiiAPI.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddScoped<IAuthService, AuthService>();


// Configs
builder.Services.AddSingleton(provider =>
    builder.Configuration.GetSection("Jwt").Get<JwtConfig>() ?? new JwtConfig());
builder.Services.AddSingleton(provider =>
    builder.Configuration.GetSection("Cors").Get<CorsConfig>() ?? new CorsConfig());

// DbContext
builder.Services.AddDbContext<OcufiiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OcufiiConnection")));

// JWT
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtConfig>();
var key = Encoding.UTF8.GetBytes(jwt.Secret);
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = true;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// CORS
var cors = builder.Configuration.GetSection("Cors").Get<CorsConfig>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowOcufii", policy =>
        policy.WithOrigins(cors.AllowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.Configure<LegacyConfig>(builder.Configuration.GetSection("LegacyConfig"));

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ocufii API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter JWT token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }},
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowOcufii");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
