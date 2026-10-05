using Einsatz.Api.Data;
using Einsatz.Api.Integrations;
using Einsatz.Api.Jobs;
using Einsatz.Api.Reports;
using Einsatz.Domain;
using Microsoft.EntityFrameworkCore;

namespace Einsatz.Api.Office;

/// <summary>
/// Office review. Approving is all-or-nothing: report PDF, ERP export and DMS archive either
/// all succeed and the job becomes <see cref="JobStatus.Exported"/>, or nothing is saved.
/// </summary>
public sealed class ApprovalService(
    IDbContextFactory<AppDb> dbs, IErpConnector erp, IDocumentArchive archive, ReportPdf pdf,
    JobReads reads, TimeProvider clock, ILogger<ApprovalService> log)
{
    public async Task<JobDto> ApproveAsync(Guid jobId, string approvedBy, CancellationToken ct = default)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await Load(db, jobId, ct);
        var now = clock.GetUtcNow();

        job.Approve(approvedBy, now);
        var report = pdf.Render(job);
        db.Integrations.Add(await erp.ExportAsync(job, now, ct));
        db.Integrations.Add(await archive.ArchiveAsync(job, report, now, ct));
        job.MarkExported();

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        log.LogInformation("Job {Number} approved by {User} and exported", job.Number, approvedBy);
        return (await reads.GetAsync(jobId, ct))!;
    }

    public async Task<JobDto> ReturnAsync(Guid jobId, string reason, CancellationToken ct = default)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var job = await Load(db, jobId, ct);
        job.ReturnToTechnician(reason);
        await db.SaveChangesAsync(ct);
        return (await reads.GetAsync(jobId, ct))!;
    }

    private static async Task<Job> Load(AppDb db, Guid id, CancellationToken ct) =>
        await db.Jobs.Full().SingleOrDefaultAsync(j => j.Id == id, ct) ?? throw new DomainException("Auftrag nicht gefunden.");
}
