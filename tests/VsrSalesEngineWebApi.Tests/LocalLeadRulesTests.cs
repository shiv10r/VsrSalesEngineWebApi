using VsrSalesEngineWebApi.Core.Services.LocalLeads;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class LocalLeadRulesTests
{
    [Theory]
    [InlineData("Sharma Clinic", "Healthcare", "clinic")]
    [InlineData("City Hospital & Research Centre", "Healthcare", "hospital")]
    [InlineData("Saraswati Vidyalaya", "Education & Coaching", "vidyalaya")]
    [InlineData("Excel IIT Coaching Classes", "Education & Coaching", "coaching")]
    [InlineData("Gupta Dhaba", "Restaurants & Cafes", "dhaba")]
    [InlineData("Annapurna Sweets", "Restaurants & Cafes", "sweets")]
    [InlineData("Verma Kirana Store", "Retail & Shops", "kirana")]
    [InlineData("Om Sai Realty", "Real Estate", "realty")]
    [InlineData("Hotel Grand Residency", "Hotels & Travel", "hotel")]
    [InlineData("Shree Ram Steel Industries", "Manufacturing & MSME", "industries")]
    [InlineData("Mishra & Associates Advocate", "Professional Services", "advocate")]
    [InlineData("Pragya Construction", "Construction & Trades", "construction")]
    [InlineData("Mahadev Construction Company", "Construction & Trades", "construction")]
    [InlineData("Ali Plumbing Works", "Construction & Trades", "plumbing")]
    [InlineData("Raju Electrician Service", "Construction & Trades", "electrician")]
    [InlineData("Singh Civil Contractors", "Construction & Trades", "contractors")]
    [InlineData("Anurag Hardware Store", "Retail & Shops", "hardware")]
    public void Categorise_PicksTheCategoryAndSaysWhy(string name, string category, string keyword)
    {
        var (actual, reason) = LocalLeadCategoriser.Categorise(name, null);

        Assert.Equal(category, actual);
        Assert.Equal($"Matched \"{keyword}\" in the name", reason);
    }

    [Fact]
    public void Categorise_ReturnsOtherWithAReasonWhenNothingMatches()
    {
        var (category, reason) = LocalLeadCategoriser.Categorise("Zeta Qwerty", null);

        Assert.Equal("Other", category);
        Assert.Contains("No keyword matched", reason);
    }

    [Fact]
    public void Categorise_FallsBackToTheCategoryTextAndSaysSo()
    {
        var (category, reason) = LocalLeadCategoriser.Categorise("Zeta Qwerty", "Dental clinic");

        Assert.Equal("Healthcare", category);
        Assert.Equal("Matched \"clinic\" in the category text", reason);
    }

    [Fact]
    public void Categorise_FirstCategoryWinsAndTheMoreSpecificKeywordIsReported()
    {
        Assert.Equal("Healthcare", LocalLeadCategoriser.Categorise("Sharma Medical Store", null).Category);
        Assert.Contains("general store", LocalLeadCategoriser.Categorise("Raj General Store", null).Reason);
    }

    [Fact]
    public void Categories_ListEndsWithOther()
    {
        Assert.Equal(10, LocalLeadCategoriser.Categories.Count);
        Assert.Equal("Other", LocalLeadCategoriser.Categories[^1]);
    }

    [Fact]
    public void Districts_Are75WithMajorOnesFirstInTheAgreedOrder()
    {
        var all = UttarPradesh.All;

        Assert.Equal(75, all.Count);
        Assert.Equal(75, all.Distinct().Count());
        Assert.Equal(new[]
        {
            UttarPradesh.Noida, "Ghaziabad", "Lucknow", "Kanpur Nagar", "Varanasi", "Prayagraj", "Agra", "Meerut",
            "Azamgarh", "Gorakhpur", "Mathura", "Bareilly", "Aligarh", "Moradabad", "Jhansi"
        }, all.Take(15).ToArray());

        var rest = all.Skip(15).ToList();
        Assert.Equal(rest.OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList(), rest);
    }

    [Theory]
    [InlineData("Noida", UttarPradesh.Noida)]
    [InlineData("greater noida", UttarPradesh.Noida)]
    [InlineData("Gautam Buddh Nagar", UttarPradesh.Noida)]
    [InlineData("Gautam Buddh Nagar (Noida)", UttarPradesh.Noida)]
    [InlineData("Kanpur", "Kanpur Nagar")]
    [InlineData("Allahabad", "Prayagraj")]
    [InlineData("lucknow district", "Lucknow")]
    [InlineData("  AZAMGARH ", "Azamgarh")]
    public void Resolve_AcceptsSpellingsAndAliases(string input, string expected) =>
        Assert.Equal(expected, UttarPradesh.Resolve(input));

    [Theory]
    [InlineData("Mumbai")]
    [InlineData("Gurgaon")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_RejectsPlacesOutsideUttarPradesh(string? input) =>
        Assert.Null(UttarPradesh.Resolve(input));

    [Theory]
    [InlineData("Sector 18, Noida, Uttar Pradesh 201301", UttarPradesh.Noida)]
    [InlineData("Hazratganj, Lucknow", "Lucknow")]
    [InlineData("Near Station Road, Kanpur Dehat", "Kanpur Dehat")]
    [InlineData("Sector 62, Noida, Delhi NCR", UttarPradesh.Noida)]
    public void DetectInAddress_FindsTheDistrict(string address, string expected) =>
        Assert.Equal(expected, UttarPradesh.DetectInAddress(address));

    [Theory]
    [InlineData("Mathura Road, New Delhi")]
    [InlineData("12 Main Street")]
    public void DetectInAddress_IgnoresOtherStatesAndNoMatch(string address) =>
        Assert.Null(UttarPradesh.DetectInAddress(address));

    [Theory]
    [InlineData("98765 43210", "9876543210")]
    [InlineData("+91-98765-43210", "9876543210")]
    [InlineData("09876543210", "9876543210")]
    [InlineData("", "")]
    public void TryPhone_NormalisesIndianNumbers(string raw, string normalized)
    {
        Assert.True(LocalLeadBuilder.TryPhone(raw, out _, out var actual));
        Assert.Equal(normalized, actual);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("98765 4321")]
    [InlineData("phone")]
    public void TryPhone_RejectsWrongLengths(string raw) =>
        Assert.False(LocalLeadBuilder.TryPhone(raw, out _, out _));

    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("http://shop.in/menu", "http://shop.in/menu")]
    [InlineData("", "")]
    public void TryWebsite_AcceptsUrlsAndAddsHttps(string raw, string expected)
    {
        Assert.True(LocalLeadBuilder.TryWebsite(raw, out var website));
        Assert.Equal(expected, website);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://files.example.com")]
    [InlineData("localhost")]
    [InlineData("javascript:alert(1)")]
    public void TryWebsite_RejectsBadUrls(string raw) =>
        Assert.False(LocalLeadBuilder.TryWebsite(raw, out _));
}
