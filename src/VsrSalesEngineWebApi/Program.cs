using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Security;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configuration options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var secretKey = jwtSection.GetValue<string>("SecretKey") ?? "VsrSalesEngineDevelopmentSecureSecretKeyMustBe32BytesLong!";
var issuer = jwtSection.GetValue<string>("Issuer") ?? "VsrSalesEngineApi";
var audience = jwtSection.GetValue<string>("Audience") ?? "VsrSalesEngineClients";

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Permitted for local development
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuthenticated", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("StandardUserOnly", policy => policy.RequireClaim("is_guest", "false"));
});

// Dependency Injection: Controller -> BLL (Service) -> Repository
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IOpportunityRepository, OpportunityRepository>();
builder.Services.AddSingleton<ISourceRegistryRepository, SourceRegistryRepository>();
builder.Services.AddSingleton<ISearchProfileRepository, SearchProfileRepository>();
builder.Services.AddSingleton<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOpportunityService, OpportunityService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IContactService, ContactService>();

// CORS configuration for local development UI
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalUi", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseCors("AllowLocalUi");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program public for integration test fixtures
public partial class Program { }
