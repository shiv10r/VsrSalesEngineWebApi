using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class ContactServiceTests
{
    [Fact]
    public async Task CreateContact_ValidEmail_CreatesSuccessfully()
    {
        var repo = new ContactRepository();
        var service = new ContactService(repo);

        var dto = new CreateContactDto(
            Name: "Alex Rivera",
            Company: "Fintech Innovations",
            Role: "VP of Engineering",
            Email: "alex.rivera@fintechinnovations.example",
            Source: "Company careers",
            SourceUrl: "https://fintechinnovations.example/team",
            ChannelBasis: "ExplicitBusinessContact",
            Notes: "Direct engineering contact"
        );

        var result = await service.CreateContactAsync(dto, "Sales Member");

        Assert.NotNull(result);
        Assert.Equal("Alex Rivera", result.Name);
        Assert.Equal("alex.rivera@fintechinnovations.example", result.Email);
        Assert.False(result.IsRoleAccount);
        Assert.Equal("Unverified", result.QualificationStatus);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at.com")]
    [InlineData("user@nodot")]
    [InlineData("user@domain.")]
    public async Task CreateContact_InvalidEmail_ThrowsArgumentException(string invalidEmail)
    {
        var repo = new ContactRepository();
        var service = new ContactService(repo);

        var dto = new CreateContactDto("Invalid Contact", "Test Co", "Lead", invalidEmail, null, null, null, null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateContactAsync(dto, "Tester"));
    }

    [Theory]
    [InlineData("info@company.com")]
    [InlineData("support@enterprise.org")]
    [InlineData("sales@platform.io")]
    [InlineData("billing@vendor.net")]
    public async Task CreateContact_RoleAccount_FlagsAndBlocksByPolicy(string roleEmail)
    {
        var repo = new ContactRepository();
        var service = new ContactService(repo);

        var dto = new CreateContactDto("Role Account", "Enterprise Co", "Support Team", roleEmail, null, null, null, null);

        var result = await service.CreateContactAsync(dto, "Tester");

        Assert.True(result.IsRoleAccount);
        Assert.Equal("BlockedByPolicy", result.QualificationStatus);
    }

    [Fact]
    public async Task CreateContact_DuplicateNormalizedEmail_DeduplicatesDeterministically()
    {
        var repo = new ContactRepository();
        var service = new ContactService(repo);

        var dto1 = new CreateContactDto("Original Name", "Company A", "Dev", "test.user@company.com", null, null, null, null);
        var created = await service.CreateContactAsync(dto1, "Tester");

        var dto2 = new CreateContactDto("Different Name", "Company B", "Dev 2", "  TEST.USER@COMPANY.COM  ", null, null, null, null);
        var duplicateAttempt = await service.CreateContactAsync(dto2, "Tester");

        Assert.Equal(created.Id, duplicateAttempt.Id);
        Assert.Equal("Original Name", duplicateAttempt.Name);
    }

    [Fact]
    public async Task QualifyContact_ValidStatus_UpdatesStatusAndAppendsNotes()
    {
        var repo = new ContactRepository();
        var service = new ContactService(repo);

        var contact = await service.CreateContactAsync(new CreateContactDto(
            "Elena Rostova", "CloudCore", "CTO", "elena@cloudcore.example", null, null, null, null
        ), "Tester");

        var qualified = await service.QualifyContactAsync(contact.Id, new QualifyContactDto(
            QualificationStatus: "Verified",
            Status: "Follow-up due",
            Notes: "Verified via LinkedIn and company board."
        ), "Reviewer");

        Assert.NotNull(qualified);
        Assert.Equal("Verified", qualified.QualificationStatus);
        Assert.Equal("Follow-up due", qualified.Status);
        Assert.Contains("Verified via LinkedIn", qualified.Notes);
    }

    [Fact]
    public async Task GetPagedContacts_FiltersProperly()
    {
        var repo = new ContactRepository();
        var service = new ContactService(repo);

        var result = await service.GetPagedContactsAsync(new ContactFilterDto(
            SearchQuery: "Maya",
            Company: null,
            Status: null,
            QualificationStatus: null,
            Page: 1,
            PageSize: 10
        ));

        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, c => c.Name.Contains("Maya"));
    }
}
