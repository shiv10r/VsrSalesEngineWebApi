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
builder.Services.AddSingleton<IOutreachTemplateRepository, OutreachTemplateRepository>();
builder.Services.AddSingleton<ISuppressionRepository, SuppressionRepository>();
builder.Services.AddSingleton<ICampaignRepository, CampaignRepository>();
builder.Services.AddSingleton<IAutomationScheduleRepository, AutomationScheduleRepository>();
builder.Services.AddSingleton<IDistributedLockService, DistributedLockService>();
builder.Services.AddSingleton<IExecutionRepository, ExecutionRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOpportunityService, OpportunityService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IOutreachDraftService, OutreachDraftService>();
builder.Services.AddScoped<IOutreachSender, OutreachSenderService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IScheduleExecutionService, ScheduleExecutionService>();
builder.Services.AddScoped<IReliabilityService, ReliabilityService>();

// LLM & AI Services (Gemini as default provider)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ILLMProvider, GeminiLlmProvider>();
builder.Services.AddSingleton<ILLMRouter, LlmRouter>();
builder.Services.AddScoped<IAiFormService, AiFormService>();

// Dynamic port binding for container environments (Vercel, Cloud Run, Docker)
var port = Environment.GetEnvironmentVariable("PORT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// CORS configuration for local development UI and deployed frontend
var configuredOrigins = builder.Configuration["AllowedOrigins"];
var allowedOriginsList = new List<string> { "http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000", "https://vsr-sales-engine-webapp.vercel.app" };
if (!string.IsNullOrWhiteSpace(configuredOrigins))
{
    allowedOriginsList.AddRange(configuredOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
              {
                  if (string.IsNullOrWhiteSpace(origin)) return false;
                  if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                  {
                      return uri.Host == "localhost" ||
                             uri.Host == "127.0.0.1" ||
                             uri.Host.EndsWith("vercel.app", StringComparison.OrdinalIgnoreCase) ||
                             allowedOriginsList.Contains(origin, StringComparer.OrdinalIgnoreCase);
                  }
                  return false;
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseCors("AllowConfiguredOrigins");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program public for integration test fixtures
public partial class Program { }
