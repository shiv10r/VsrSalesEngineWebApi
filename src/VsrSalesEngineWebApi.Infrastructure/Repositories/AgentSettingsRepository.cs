using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class AgentSettingsRepository : IAgentSettingsRepository
{
    private const int RowId = 1;
    private readonly VsrDbContext _context;

    public AgentSettingsRepository(VsrDbContext context)
    {
        _context = context;
    }

    public Task<AgentSettings?> GetAsync() =>
        _context.AgentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == RowId);

    public async Task SaveAsync(AgentSettings settings)
    {
        var existing = await _context.AgentSettings.FirstOrDefaultAsync(s => s.Id == RowId);
        if (existing == null)
        {
            settings.Id = RowId;
            _context.AgentSettings.Add(settings);
        }
        else
        {
            existing.DraftThreshold = settings.DraftThreshold;
            existing.AgentsJson = settings.AgentsJson;
            existing.UpdatedBy = settings.UpdatedBy;
            existing.UpdatedAtUtc = settings.UpdatedAtUtc;
        }

        await _context.SaveChangesAsync();
    }
}
