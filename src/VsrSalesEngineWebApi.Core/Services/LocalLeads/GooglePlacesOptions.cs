namespace VsrSalesEngineWebApi.Core.Services.LocalLeads;

/// <summary>Bound from GooglePlaces:* (env GooglePlaces__ApiKey). An empty key switches the feature off.</summary>
public class GooglePlacesOptions
{
    public const string SectionName = "GooglePlaces";
    public const int DefaultDailyCap = 100;

    public string ApiKey { get; set; } = string.Empty;
    public int DailyRequestCap { get; set; } = DefaultDailyCap;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
