using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Security;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

[Collection("OutreachSenderStateCollection")]
public class ApiIntegrationAndConstraintTests
{
    private readonly IOpportunityRepository _opportunityRepo;
    private readonly IOpportunityService _opportunityService;
    private readonly IContactRepository _contactRepo;
    private readonly IContactService _contactService;
    private readonly OutreachSenderService _outreachSender;
    private readonly ISuppressionRepository _suppressionRepo;
    private readonly IAuthService _authService;

    public ApiIntegrationAndConstraintTests()
    {
        _opportunityRepo = new OpportunityRepository();
        _opportunityService = new OpportunityService(_opportunityRepo);
        _contactRepo = new ContactRepository();
        _contactService = new ContactService(_contactRepo);
        _suppressionRepo = new SuppressionRepository();
        _outreachSender = new OutreachSenderService(_suppressionRepo);

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(r => r.GetByEmailAsync("sales@vsr.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = Guid.NewGuid(),
                Email = "sales@vsr.local",
                PasswordHash = "ValidSecret123!",
                FullName = "VSR Sales",
                Role = "SalesMember",
                IsGuest = false
            });

        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Development");

        var jwtOptions = Options.Create(new JwtOptions
        {
            SecretKey = "VsrSalesEngineTestingSecretKeyMustBe32BytesLong!",
            Issuer = "VsrTest",
            Audience = "VsrTestClients",
            ExpirationMinutes = 60
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:AllowDevelopmentGuest"] = "true"
            })
            .Build();

        _authService = new AuthService(userRepoMock.Object, jwtOptions, envMock.Object, config);
    }

    [Fact]
    public async Task ExternalSourceDeduplication_IngestingDuplicateUrlReturnsSameRecord()
    {
        var url = "https://careers.company.example/jobs/dotnet-architect-101";

        var opp1 = await _opportunityService.CreateOpportunityAsync(new CreateOpportunityDto(
            Company: "Company Example",
            Title: ".NET Architect",
            Source: "Company careers",
            Location: "Remote",
            Fit: "High",
            Type: "Individual developer",
            HasVendorDocRequirements: false,
            VendorRequirementNotes: null,
            SourceUrl: url,
            EvidenceSnippet: "Original posting snippet.",
            MatchedQuery: ".NET Architect"
        ), "sync-worker");

        // Attempting to ingest the same canonical URL again with same company and title
        var opp2 = await _opportunityService.CreateOpportunityAsync(new CreateOpportunityDto(
            Company: "Company Example",
            Title: ".NET Architect",
            Source: "Company careers",
            Location: "Remote",
            Fit: "High",
            Type: "Individual developer",
            HasVendorDocRequirements: false,
            VendorRequirementNotes: null,
            SourceUrl: url,
            EvidenceSnippet: "Duplicate ingest snippet.",
            MatchedQuery: ".NET Architect"
        ), "sync-worker");

        // Canonical deduplication returns existing ID rather than creating a duplicate
        Assert.Equal(opp1.Id, opp2.Id);
    }

    [Fact]
    public async Task PagingAndFiltering_ReturnsBoundedPageResults()
    {
        var filter = new OpportunityFilterDto(
            SearchQuery: null,
            Status: "New",
            Source: null,
            Fit: null,
            HasVendorDocs: null,
            Page: 1,
            PageSize: 2
        );

        var pagedResults = await _opportunityService.GetPagedOpportunitiesAsync(filter);

        Assert.NotNull(pagedResults);
        Assert.True(pagedResults.Items.Count <= 2);
    }

    [Fact]
    public async Task ContactDeduplication_ByCompanyAndEmail()
    {
        var c1 = await _contactService.CreateContactAsync(new CreateContactDto(
            Name: "Kavita Rao",
            Company: "Acme Corp",
            Role: "VP Engineering",
            Email: "kavita.rao@acme.example",
            Source: "Direct research",
            SourceUrl: "https://acme.example/about",
            ChannelBasis: "ExplicitBusinessContact",
            Notes: "Publicly listed"
        ), "sales-rep-1");

        // Ingesting same email for same company
        var c2 = await _contactService.CreateContactAsync(new CreateContactDto(
            Name: "Kavita Rao Duplicate",
            Company: "Acme Corp",
            Role: "VP Engineering",
            Email: "kavita.rao@acme.example",
            Source: "Job board",
            SourceUrl: "https://acme.example/careers",
            ChannelBasis: "ExplicitBusinessContact",
            Notes: "Repeated entry"
        ), "sales-rep-2");

        Assert.Equal(c1.Id, c2.Id);
    }

    [Fact]
    public async Task IdempotentOutreachSend_DuplicateKeysDoNotDuplicateSend()
    {
        var idempotencyKey = $"IDEMP-{Guid.NewGuid():N}";

        var request1 = new OutreachSendRequest(
            RecipientEmail: "target.lead@example.com",
            RecipientName: "Target Lead",
            Subject: "Engineering capacity for .NET team",
            Body: "Hi Target,\n\nWe would love to assist with your .NET modernization.",
            IdempotencyKey: idempotencyKey,
            IsTestMode: true,
            SinkAddressOverride: null
        );

        var result1 = await _outreachSender.SendOutreachAsync(request1);
        Assert.True(result1.Success);
        Assert.True(result1.WasRoutedToSink);

        // Send duplicate request with identical idempotency key
        var result2 = await _outreachSender.SendOutreachAsync(request1);
        Assert.True(result2.Success);
        Assert.Equal(result1.MessageId, result2.MessageId);
        Assert.Equal(result1.ProviderMessageId, result2.ProviderMessageId);
    }

    [Fact]
    public async Task AuthTokenValidation_StandardVsGuestClaims()
    {
        // 1. Standard user login
        var standardLogin = await _authService.LoginAsync(new LoginRequestDto("sales@vsr.local", "ValidSecret123!"));
        Assert.NotNull(standardLogin);
        Assert.True(standardLogin.Success);
        Assert.NotNull(standardLogin.User);
        Assert.False(standardLogin.User.IsGuest);
        Assert.NotEmpty(standardLogin.Token!);

        // 2. Guest user session
        var guestSession = await _authService.CreateGuestSessionAsync();
        Assert.NotNull(guestSession);
        Assert.True(guestSession.Success);
        Assert.NotNull(guestSession.User);
        Assert.True(guestSession.User.IsGuest);
        Assert.NotEmpty(guestSession.Token!);
    }
}
