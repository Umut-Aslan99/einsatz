using Einsatz.Api.Data;
using Einsatz.Domain;
using Microsoft.EntityFrameworkCore;

namespace Einsatz.Api.Jobs;

public sealed class JobReads(IDbContextFactory<AppDb> dbs)
{
    public async Task<List<JobDto>> ListAsync(Func<IQueryable<Job>, IQueryable<Job>> filter, CancellationToken ct = default)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var jobs = await filter(db.Jobs.AsNoTracking())
            .OrderBy(j => j.ScheduledFor).ThenBy(j => j.Number)
            .Select(JobDto.Projection).AsSplitQuery().ToListAsync(ct);
        if (jobs.Count == 0) return jobs;

        var ids = jobs.Select(j => j.Id).ToList();
        var integrations = await db.Integrations.AsNoTracking()
            .Where(i => ids.Contains(i.JobId)).OrderBy(i => i.CreatedAt)
            .Select(i => new { i.JobId, Dto = new IntegrationDto(i.Id, i.Target, i.FileName, i.CreatedAt) })
            .ToListAsync(ct);
        var byJob = integrations.ToLookup(i => i.JobId, i => i.Dto);
        return jobs.Select(j => j with { Integrations = byJob[j.Id].ToList() }).ToList();
    }

    public async Task<JobDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        (await ListAsync(q => q.Where(j => j.Id == id), ct)).SingleOrDefault();

    public async Task<List<ArticleDto>> ArticlesAsync(CancellationToken ct = default)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        return await db.Articles.AsNoTracking().OrderBy(a => a.Number)
            .Select(a => new ArticleDto(a.Number, a.Name, a.Unit, a.Price)).ToListAsync(ct);
    }
}
