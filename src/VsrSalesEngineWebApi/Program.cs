using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Security;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;
using VsrSalesEngineWebApi.Core.Services.LocalLeads;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using VsrSalesEngineWebApi.Infrastructure.Services;
using VsrSalesEngineWebApi.Places;
using VsrSalesEngineWebApi.Infrastructure.Services.PublicWeb;
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
    // Every endpoint needs a signed-in user unless it is explicitly [AllowAnonymous].
    options.RequireAuthenticatedByDefault();
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
builder.Services.AddScoped<IAutomationScheduleRepository, AutomationScheduleRepository>();
builder.Services.AddScoped<IDistributedLockService, DistributedLockService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOpportunityService, OpportunityService>();
builder.Services.AddScoped<ILiveJobFeedService, LiveJobFeedService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<ILocalLeadRepository, LocalLeadRepository>();
builder.Services.AddScoped<ILocalLeadService, LocalLeadService>();
// Real-time Google Maps search through the official Places API (New). Empty GooglePlaces:ApiKey = feature off.
var placesOptions = new GooglePlacesOptions();
builder.Configuration.GetSection(GooglePlacesOptions.SectionName).Bind(placesOptions);
builder.Services.AddSingleton(placesOptions);
builder.Services.AddSingleton<PlacesUsageLimiter>();
builder.Services.AddHttpClient(GooglePlacesClient.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(12));
builder.Services.AddScoped<IPlacesClient, GooglePlacesClient>();
builder.Services.AddScoped<IPlaceDiscoveryService, PlaceDiscoveryService>();
builder.Services.AddScoped<IScheduleExecutionService, ScheduleExecutionService>();
builder.Services.AddScoped<IDueScheduleRunner, DueScheduleRunner>();

// Agent relay: six agents write a persisted, human-readable activity log while they work.
builder.Services.AddSingleton(new AgentRelayOptions
{
    StepPauseMs = builder.Configuration.GetValue("Agents:StepPauseMs", 300) // UI pacing only
});
// People connected to a post: public company pages only, fetched through a guarded client.
builder.Services.AddSingleton<IHostResolver, SystemHostResolver>();
builder.Services.AddSingleton<HostThrottle>();
builder.Services.AddHttpClient<IPublicPageFetcher, PublicPageFetcher>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        MaxResponseHeadersLength = 32,
        ConnectCallback = SafeSocketConnector.ConnectAsync
    });
builder.Services.AddScoped<IRelatedPeopleFinder, RelatedPeopleFinder>();
builder.Services.AddScoped<IAgentRunRepository, AgentRunRepository>();
builder.Services.AddScoped<IAgentRelay, AgentRelayService>();
builder.Services.AddScoped<IAgentSettingsRepository, AgentSettingsRepository>();
builder.Services.AddScoped<IAgentSettingsService, AgentSettingsService>();
builder.Services.AddScoped<IAgentActivityService, AgentActivityService>();
builder.Services.AddScoped<ILeadHeatmapService, LeadHeatmapService>();
builder.Services.AddSingleton<IManualRunLauncher, ManualRunLauncher>();

// Dynamic port binding for container environments (Vercel, Cloud Run, Docker)
var port = Environment.GetEnvironmentVariable("PORT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// CORS: exact-origin allowlist (AllowedOrigins env var + the production frontend; localhost only in Development).
// Bearer tokens are sent in a header, so credentialed CORS is not needed.
var corsOrigins = new CorsOrigins(builder.Configuration["AllowedOrigins"], builder.Environment.IsDevelopment());
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins", policy =>
        policy.SetIsOriginAllowed(corsOrigins.IsAllowed).AllowAnyHeader().AllowAnyMethod());
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
