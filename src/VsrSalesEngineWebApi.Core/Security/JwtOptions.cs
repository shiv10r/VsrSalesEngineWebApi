namespace VsrSalesEngineWebApi.Core.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "VsrSalesEngineApi";
    public string Audience { get; set; } = "VsrSalesEngineClients";
    public string SecretKey { get; set; } = "VsrSalesEngineSecureDefaultSecretKeyMustBe32BytesLong!";
    public int ExpirationMinutes { get; set; } = 120;
}

