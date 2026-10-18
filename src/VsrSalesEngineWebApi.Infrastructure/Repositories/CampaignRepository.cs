using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class CampaignRepository : ICampaignRepository
{
    private static readonly ConcurrentDictionary<string, Campaign> Campaigns = new(StringComparer.OrdinalIgnoreCase);

    static CampaignRepository()
    {
        var seed = new List<Campaign>
        {
            new()
            {
                Id = "CMP-001",
                Name = ".NET API Modernization Outreach",
                Audience = "Healthcare & Fintech CTOs",
                Mode = "ApprovalRequired",
                Status = "Active",
                DailySendLimit = 30,
                CurrentDaySent = 8,
                MaxTouches = 3,
                DelayDaysBetweenTouches = 3,
                StopOnReply = true,
                StopOnOptOut = true,
                StopOnBounce = true,
                SelectedTemplateId = "TPL-001",
                SenderName = "VSR Outreach Lead",
                SenderEmail = "outreach@vsrtechnologies.com",
                SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701",
                Jurisdiction = "US-CANSPAM",
                OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out",
                TotalSent = 142,
                TotalReplies = 18,
                TotalOptOuts = 2,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-14),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = "CMP-002",
                Name = "Cloud Infrastructure Augmentation",
                Audience = "Scale-ups in US/UK",
                Mode = "DraftOnly",
                Status = "Paused",
                DailySendLimit = 20,
                CurrentDaySent = 0,
                MaxTouches = 2,
                DelayDaysBetweenTouches = 4,
                StopOnReply = true,
                StopOnOptOut = true,
                StopOnBounce = true,
                SelectedTemplateId = "TPL-002",
                SenderName = "VSR Client Partner",
                SenderEmail = "partners@vsrtechnologies.com",
                SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701",
                Jurisdiction = "US-CANSPAM",
                OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out",
                TotalSent = 89,
                TotalReplies = 7,
                TotalOptOuts = 1,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Id = "CMP-003",
                Name = "Public Procurement Capability Brief",
                Audience = "Public Sector Tender Leads",
                Mode = "DraftOnly",
                Status = "Draft",
                DailySendLimit = 15,
                CurrentDaySent = 0,
                MaxTouches = 2,
                DelayDaysBetweenTouches = 5,
                StopOnReply = true,
                StopOnOptOut = true,
                StopOnBounce = true,
                SelectedTemplateId = "TPL-003",
                SenderName = "VSR Public Sector Lead",
                SenderEmail = "procurement@vsrtechnologies.com",
                SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701",
                Jurisdiction = "US-CANSPAM",
                OptOutMechanismUrl = "https://vsrtechnologies.com/outreach/opt-out",
                TotalSent = 0,
                TotalReplies = 0,
                TotalOptOuts = 0,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-5)
            }
        };

        foreach (var c in seed)
        {
            Campaigns[c.Id] = c;
        }
    }

    public Task<IReadOnlyList<Campaign>> GetAllAsync(string? status = null)
    {
        var q = Campaigns.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(c => c.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        var list = q.OrderByDescending(c => c.UpdatedAtUtc).ToList();
        return Task.FromResult<IReadOnlyList<Campaign>>(list);
    }

    public Task<Campaign?> GetByIdAsync(string id)
    {
        Campaigns.TryGetValue(id, out var campaign);
        return Task.FromResult(campaign);
    }

    public Task<Campaign> CreateAsync(Campaign campaign)
    {
        Campaigns[campaign.Id] = campaign;
        return Task.FromResult(campaign);
    }

    public Task<Campaign?> UpdateAsync(Campaign campaign)
    {
        if (!Campaigns.ContainsKey(campaign.Id)) return Task.FromResult<Campaign?>(null);
        Campaigns[campaign.Id] = campaign;
        return Task.FromResult<Campaign?>(campaign);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var removed = Campaigns.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}
