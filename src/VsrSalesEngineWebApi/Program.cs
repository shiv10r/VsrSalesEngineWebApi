using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Security;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using VsrSalesEngineWebApi.Infrastructure.Services;
using VsrSalesEngineWebApi.Security;
using VsrSalesEngineWebApi.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configuration options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var secretKey = jwtSection.GetValue<string>("SecretKey") ?? "VsrSalesEngineDevelopmentSecureSecretKeyMustBe32BytesLong!";
var issuer = jwtSection.GetValue<string>("Issuer") ?? "VsrSalesEngineApi";
var audience = jwtSection.GetValue<string>("Audience") ?? "VsrSalesEngineClients";
SecurityExtensions.EnsureStrongJwtSecret(secretKey, builder.Environment);

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

// Persistence: Supabase/PostgreSQL in production, EF InMemory fallback for zero-config local dev.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<VsrDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseInMemoryDatabase("VsrSalesEngineDevFallback");
    }
});

// Dependency Injection: Controller -> BLL (Service) -> Repository
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOpportunityRepository, OpportunityRepository>();
builder.Services.AddScoped<ISourceRegistryRepository, SourceRegistryRepository>();
builder.Services.AddScoped<ISearchProfileRepository, SearchProfileRepository>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IOutreachTemplateRepository, OutreachTemplateRepository>();
builder.Services.AddScoped<ISuppressionRepository, SuppressionRepository>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<IAutomationScheduleRepository, AutomationScheduleRepository>();
builder.Services.AddScoped<IDistributedLockService, DistributedLockService>();
builder.Services.AddScoped<IExecutionRepository, ExecutionRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOpportunityService, OpportunityService>();
builder.Services.AddScoped<ILiveJobFeedService, LiveJobFeedService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IOutreachDraftService, OutreachDraftService>();
builder.Services.AddScoped<IOutreachSender, OutreachSenderService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IScheduleExecutionService, ScheduleExecutionService>();
builder.Services.AddScoped<IDueScheduleRunner, DueScheduleRunner>();

// Agent relay: eight agents write a persisted, human-readable activity log while they work.
builder.Services.AddSingleton(new AgentRelayOptions
{
    StepPauseMs = builder.Configuration.GetValue("Agents:StepPauseMs", 300), // UI pacing only
    DraftThreshold = builder.Configuration.GetValue("Agents:DraftThreshold", 50)
});
builder.Services.AddScoped<IAgentRunRepository, AgentRunRepository>();
builder.Services.AddScoped<IAgentRelay, AgentRelayService>();
builder.Services.AddScoped<IAgentActivityService, AgentActivityService>();
builder.Services.AddScoped<ILeadHeatmapService, LeadHeatmapService>();
builder.Services.AddSingleton<IManualRunLauncher, ManualRunLauncher>();
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

builder.Services.AddApiRateLimiting();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Autonomous lead-hunting worker: polls enabled schedules and runs live discovery on a timer.
// Runs in-process in the web host; the DB-backed distributed lock keeps it safe if multiple instances run.
builder.Services.AddHostedService<DiscoveryWorkerHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VsrDbContext>();
    db.Database.EnsureCreated();
    // Demo logins exist only in Development. Elsewhere set Seed__AdminEmail and Seed__AdminPassword
    // (12+ characters) to create one Admin with a hashed password; otherwise no users are created.
    var userSeed = UserSeedOptions.Resolve(
        app.Environment.IsDevelopment(),
        builder.Configuration["Seed:AdminEmail"],
        builder.Configuration["Seed:AdminPassword"]);
    await VsrDbSeeder.SeedAsync(db, userSeed);
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseGenericErrorResponses();
}
app.UseSecurityHeaders(enableHsts: !app.Environment.IsDevelopment());
app.UseCors("AllowConfiguredOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program public for integration test fixtures
public partial class Program { }
