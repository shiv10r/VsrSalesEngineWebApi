using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Services.LocalLeads;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using VsrSalesEngineWebApi.Security;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class LocalLeadServiceTests
{
    private readonly VsrDbContext _context = TestDbContextFactory.CreateSeeded();
    private readonly LocalLeadService _service;

    public LocalLeadServiceTests()
    {
        _service = new LocalLeadService(new LocalLeadRepository(_context));
    }

    private static ImportLocalLeadRowDto Row(string? name, string? district = "Lucknow", string? phone = null,
        string? address = null, string? website = null, double? rating = null, string? category = null) =>
        new(name, district, category, address, phone, website, rating, null);

    private Task<ImportLocalLeadsResultDto> Import(params ImportLocalLeadRowDto[] rows) =>
        _service.ImportAsync(new ImportLocalLeadsRequestDto("Test file", rows.ToList()), "tester");

    [Fact]
    public async Task Nothing_IsSeeded()
    {
        var summary = await _service.GetSummaryAsync();

        Assert.Equal(0, summary.Total);
        Assert.Empty(summary.Grid);
    }

    [Fact]
    public async Task Import_SavesValidRowsWithCategoryReasonAndSource()
    {
        var result = await Import(
            Row("Sharma Clinic", "Lucknow", "98765 43210", website: "sharmaclinic.in", rating: 4.5),
            Row("Gupta Dhaba", "Azamgarh"));

        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Duplicates);
        Assert.Equal(0, result.Skipped);

        var page = await _service.GetPagedAsync(new LocalLeadFilterDto(null, null, null, null, null));
        var clinic = page.Items.Single(l => l.BusinessName == "Sharma Clinic");
        Assert.Equal("Healthcare", clinic.Category);
        Assert.Equal("Matched \"clinic\" in the name", clinic.CategoryReason);
        Assert.Equal("CSV import", clinic.Source);
        Assert.Equal("Test file", clinic.SourceLabel);
        Assert.Equal("New", clinic.Status);
        Assert.True(clinic.HasWebsite);
        Assert.Equal("https://sharmaclinic.in", clinic.Website);
        Assert.Equal("tester", clinic.CreatedBy);
    }

    [Fact]
    public async Task Import_SkipsDuplicateByNameAndDistrictEvenWithDifferentPunctuation()
    {
        await Import(Row("Sharma Clinic", "Lucknow"));

        var result = await Import(
            Row("sharma  clinic.", "Lucknow"),     // same lead
            Row("Sharma Clinic", "Varanasi"));      // same name, other district: allowed

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Duplicates);
    }

    [Fact]
    public async Task Import_SkipsDuplicateByPhoneAndDuplicatesInsideTheSameFile()
    {
        await Import(Row("Alpha Store", "Lucknow", "9876543210"));

        var result = await Import(
            Row("Beta Store", "Agra", "+91 98765 43210"),   // phone already stored
            Row("Gamma Store", "Agra", "9123456789"),
            Row("Gamma Store", "Agra", "9000000000"));      // same name+district as the row above

        Assert.Equal(1, result.Imported);
        Assert.Equal(2, result.Duplicates);
    }

    [Fact]
    public async Task Import_ReportsRowNumbersForEveryKindOfBadRow()
    {
        var result = await Import(
            Row("Good Shop", "Lucknow"),                                 // 1 ok
            Row("  ", "Lucknow"),                                        // 2 no name
            Row("No District Shop", null),                               // 3 no district
            Row("Delhi Shop", "Mumbai", address: "Fort, Mumbai"),        // 4 not UP
            Row("Bad Phone Shop", "Lucknow", "12345"),                   // 5 phone
            Row("Bad Site Shop", "Lucknow", website: "not a url"),       // 6 website
            Row("Bad Rating Shop", "Lucknow", rating: 7));               // 7 rating

        Assert.Equal(1, result.Imported);
        Assert.Equal(6, result.Skipped);
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, result.Errors.Select(e => e.Row).ToArray());
        Assert.Equal("businessName is required", result.Errors[0].Message);
        Assert.Equal("district missing or not in Uttar Pradesh", result.Errors[1].Message);
        Assert.Equal("district missing or not in Uttar Pradesh", result.Errors[2].Message);
        Assert.Equal("phone must have 10 digits", result.Errors[3].Message);
        Assert.Equal("website is not a valid URL", result.Errors[4].Message);
        Assert.Equal("rating must be between 0 and 5", result.Errors[5].Message);
    }

    [Fact]
    public async Task Import_DetectsTheDistrictFromTheAddressWhenTheColumnIsEmpty()
    {
        var result = await Import(Row("Raj Mobiles", null, address: "Sector 18, Noida, Uttar Pradesh"));

        Assert.Equal(1, result.Imported);
        var lead = (await _service.GetPagedAsync(new LocalLeadFilterDto(null, null, null, null, null))).Items.Single();
        Assert.Equal(UttarPradesh.Noida, lead.District);
    }

    [Fact]
    public async Task Import_UsesAGivenKnownCategoryAsIs()
    {
        await Import(Row("Zeta Qwerty", "Lucknow", category: "real estate"));

        var lead = (await _service.GetPagedAsync(new LocalLeadFilterDto(null, null, null, null, null))).Items.Single();
        Assert.Equal("Real Estate", lead.Category);
        Assert.Equal("Category was provided", lead.CategoryReason);
    }

    [Fact]
    public async Task Import_RejectsMoreThan2000RowsAndEmptyRequests()
    {
        var tooMany = Enumerable.Range(0, 2001).Select(i => Row($"Shop {i}")).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ImportAsync(new ImportLocalLeadsRequestDto("x", tooMany), "t"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ImportAsync(new ImportLocalLeadsRequestDto("x", new()), "t"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ImportAsync(new ImportLocalLeadsRequestDto("x", null), "t"));
        Assert.Equal(0, (await _service.GetSummaryAsync()).Total);
    }

    [Fact]
    public async Task Import_Accepts2000Rows()
    {
        var rows = Enumerable.Range(0, 2000).Select(i => Row($"Shop Number {i}", "Lucknow")).ToArray();

        var result = await Import(rows);

        Assert.Equal(2000, result.Imported);
    }

    private async Task SeedForListing()
    {
        await Import(
            Row("Sharma Clinic", "Lucknow", "9876543210", website: "sharma.in"),
            Row("Verma Dental Clinic", "Lucknow"),
            Row("Gupta Dhaba", "Azamgarh"),
            Row("Raj Kirana Store", "Azamgarh", "9111111111"),
            Row("Om Realty", UttarPradesh.Noida));
    }

    [Fact]
    public async Task List_FiltersByDistrictCategoryStatusWebsiteAndSearch()
    {
        await SeedForListing();
        Task<LocalLeadPageDto> Query(string? d = null, string? c = null, string? s = null, bool? w = null, string? q = null) =>
            _service.GetPagedAsync(new LocalLeadFilterDto(d, c, s, w, q));

        Assert.Equal(2, (await Query(d: "lucknow")).Total);
        Assert.Equal(1, (await Query(d: "Noida")).Total);              // alias resolves
        Assert.Equal(2, (await Query(c: "Healthcare")).Total);
        Assert.Equal(1, (await Query(d: "Lucknow", w: true)).Total);
        Assert.Equal(4, (await Query(w: false)).Total);
        Assert.Equal(1, (await Query(q: "kirana")).Total);
        Assert.Equal(1, (await Query(q: "9111111111")).Total);
        Assert.Equal(5, (await Query(s: "new")).Total);
        Assert.Equal(0, (await Query(s: "Won")).Total);
    }

    [Fact]
    public async Task List_PagesAndClampsPageSize()
    {
        await Import(Enumerable.Range(0, 30).Select(i => Row($"Shop Number {i}")).ToArray());

        var first = await _service.GetPagedAsync(new LocalLeadFilterDto(null, null, null, null, null, 1, 25));
        var second = await _service.GetPagedAsync(new LocalLeadFilterDto(null, null, null, null, null, 2, 25));
        var clampedHigh = await _service.GetPagedAsync(new LocalLeadFilterDto(null, null, null, null, null, 1, 5000));
        var clampedLow = await _service.GetPagedAsync(new LocalLeadFilterDto(null, null, null, null, null, 0, 0));

        Assert.Equal(30, first.Total);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(5, second.Items.Count);
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
        Assert.Equal(100, clampedHigh.PageSize);
        Assert.Equal(1, clampedLow.PageSize);
        Assert.Equal(1, clampedLow.Page);
    }

    [Fact]
    public async Task Summary_CountsByDistrictCategoryAndGridWithoutZeroCells()
    {
        await SeedForListing();

        var summary = await _service.GetSummaryAsync();

        Assert.Equal(5, summary.Total);
        Assert.Equal(4, summary.NoWebsite);
        Assert.Equal(2, summary.ByDistrict.Single(d => d.District == "Lucknow").Count);
        Assert.Equal(2, summary.ByCategory.Single(c => c.Category == "Healthcare").Count);
        Assert.Equal(2, summary.Grid.Single(g => g.District == "Lucknow" && g.Category == "Healthcare").Count);
        Assert.All(summary.Grid, g => Assert.True(g.Count > 0));
        Assert.Equal(5, summary.Grid.Sum(g => g.Count));
    }

    [Fact]
    public void Options_ListDistrictsCategoriesAndStatuses()
    {
        var options = _service.GetOptions();

        Assert.Equal(75, options.Districts.Count);
        Assert.Equal(9, options.Categories.Count);
        Assert.Equal(new[] { "New", "Contacted", "Interested", "Won", "Lost" }, options.Statuses);
    }

    [Fact]
    public async Task Create_SavesAManualLeadAndAutoCategorises()
    {
        var lead = await _service.CreateAsync(new CreateLocalLeadDto("Singh Coaching Classes", "Varanasi"), "tester");

        Assert.Equal("Manual", lead.Source);
        Assert.Equal("Education & Coaching", lead.Category);
        Assert.False(lead.HasWebsite);
    }

    [Fact]
    public async Task Create_RejectsInvalidAndDuplicateLeads()
    {
        await _service.CreateAsync(new CreateLocalLeadDto("Singh Coaching Classes", "Varanasi", Phone: "9876543210"), "tester");

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(new CreateLocalLeadDto("", "Varanasi"), "t"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(new CreateLocalLeadDto("X Shop", "Mumbai"), "t"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(new CreateLocalLeadDto("singh coaching classes", "Varanasi"), "t"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(new CreateLocalLeadDto("Other Name", "Agra", Phone: "98765 43210"), "t"));
    }

    [Fact]
    public async Task Update_ChangesStatusNotesAndWebsiteAndRejectsBadValues()
    {
        var lead = await _service.CreateAsync(new CreateLocalLeadDto("Raj Kirana Store", "Agra"), "tester");

        var updated = await _service.UpdateAsync(lead.Id, new UpdateLocalLeadDto("contacted", "Called on Monday", "Retail & Shops", null, null, "rajstore.in"));

        Assert.NotNull(updated);
        Assert.Equal("Contacted", updated!.Status);
        Assert.Equal("Called on Monday", updated.Notes);
        Assert.True(updated.HasWebsite);
        Assert.Equal("Set by a person", updated.CategoryReason);

        var cleared = await _service.UpdateAsync(lead.Id, new UpdateLocalLeadDto(Website: ""));
        Assert.False(cleared!.HasWebsite);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(lead.Id, new UpdateLocalLeadDto(Status: "Maybe")));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(lead.Id, new UpdateLocalLeadDto(Category: "Spaceships")));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(lead.Id, new UpdateLocalLeadDto(District: "Mumbai")));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(lead.Id, new UpdateLocalLeadDto(Phone: "123")));
        Assert.Null(await _service.UpdateAsync("LL-MISSING", new UpdateLocalLeadDto(Status: "Won")));
    }

    [Fact]
    public async Task Delete_RemovesTheLeadOrReportsNotFound()
    {
        var lead = await _service.CreateAsync(new CreateLocalLeadDto("Raj Kirana Store", "Agra"), "tester");

        Assert.True(await _service.DeleteAsync(lead.Id));
        Assert.False(await _service.DeleteAsync(lead.Id));
        Assert.Equal(0, (await _service.GetSummaryAsync()).Total);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Import")]
    public void WriteEndpoints_CarryTheGuestGuard(string action)
    {
        var method = typeof(LocalLeadsController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;

        Assert.NotNull(method.GetCustomAttribute<DisallowGuestAttribute>());
        Assert.NotNull(method.GetCustomAttributes().OfType<HttpMethodAttribute>().FirstOrDefault(a => a is not HttpGetAttribute));
    }

    [Fact]
    public void ReadEndpoints_AreOpenToGuests()
    {
        foreach (var action in new[] { "GetOptions", "GetPaged", "GetSummary" })
        {
            var method = typeof(LocalLeadsController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;
            Assert.Null(method.GetCustomAttribute<DisallowGuestAttribute>());
        }
    }
}
