using VsrSalesEngineWebApi.Security;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class CorsOriginsTests
{
    [Theory]
    [InlineData("https://vsr-sales-engine-webapp.vercel.app")]
    [InlineData("https://VSR-Sales-Engine-Webapp.vercel.app")]
    public void ProductionFrontend_IsAlwaysAllowed(string origin) =>
        Assert.True(new CorsOrigins(null, isDevelopment: false).IsAllowed(origin));

    [Theory]
    [InlineData("https://evilvercel.app")]
    [InlineData("https://x.vercel.app")]
    [InlineData("https://vsr-sales-engine-webapp.vercel.app.evil.example")]
    [InlineData("http://vsr-sales-engine-webapp.vercel.app")]
    [InlineData("https://vercel.app")]
    [InlineData("null")]
    [InlineData("")]
    public void LookalikeAndForeignOrigins_AreRejected(string origin) =>
        Assert.False(new CorsOrigins(null, isDevelopment: true).IsAllowed(origin));

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1:5173")]
    public void LocalOrigins_OnlyInDevelopment(string origin)
    {
        Assert.True(new CorsOrigins(null, isDevelopment: true).IsAllowed(origin));
        Assert.False(new CorsOrigins(null, isDevelopment: false).IsAllowed(origin));
        Assert.False(new CorsOrigins("https://app.example.com", isDevelopment: false).IsAllowed(origin));
    }

    [Fact]
    public void ConfiguredOrigins_AreMatchedExactly()
    {
        var cors = new CorsOrigins(" https://app.example.com/ , https://other.example.org ,not a url, https://path.example.com/x", false);

        Assert.True(cors.IsAllowed("https://app.example.com"));
        Assert.True(cors.IsAllowed("https://other.example.org"));
        Assert.False(cors.IsAllowed("https://app.example.com.evil.example"));
        Assert.False(cors.IsAllowed("https://sub.app.example.com"));
        Assert.False(cors.IsAllowed("https://path.example.com"));
    }
}
