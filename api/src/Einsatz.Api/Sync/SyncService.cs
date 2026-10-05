using System.Text.Json;
using Einsatz.Api.Data;
using Einsatz.Api.Jobs;
using Einsatz.Domain;
using Microsoft.EntityFrameworkCore;

namespace Einsatz.Api.Sync;

/// <summary>
/// Applies offline commands in the order they were recorded. Each command is idempotent
/// (tracked in <see cref="ProcessedCommand"/>), so a device can safely retry a whole batch
/// after a dropped connection. Rule violations reject only that command, not the batch.
/// </summary>
public sealed class SyncService(IDbContextFactory<AppDb> dbs, JobReads reads, TimeProvider clock, ILogger<SyncService> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PullResponse> PullAsync(Guid technicianId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().Date);
        var jobs = await reads.ListAsync(q => q.Where(j =>
            j.TechnicianId == technicianId &&
            (j.ScheduledFor >= today.AddDays(-7) || j.Status == JobStatus.InProgress)), ct);
        return new PullResponse(clock.GetUtcNow(), jobs, await reads.ArticlesAsync(ct));
    }

    public async Task<PushResponse> PushAsync(Guid technicianId, IReadOnlyList<SyncCommand> commands, CancellationToken ct = default)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var ids = commands.Select(c => c.Id).ToList();
        var seen = await db.ProcessedCommands.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var jobs = new Dictionary<Guid, Job?>();
        var results = new List<CommandResult>(commands.Count);

        foreach (var cmd in commands)
        {
            if (seen.TryGetValue(cmd.Id, out var prior))
            {
                // Replay the original verdict so the device ends up in the same state either way.
                results.Add(prior.Outcome == Outcome.Rejected
                    ? new CommandResult(cmd.Id, Outcome.Rejected, prior.Error)
                    : new CommandResult(cmd.Id, Outcome.Duplicate));
                continue;
            }

            if (!jobs.TryGetValue(cmd.JobId, out var job))
            {
                job = await db.Jobs.Full().SingleOrDefaultAsync(j => j.Id == cmd.JobId && j.TechnicianId == technicianId, ct);
                jobs[cmd.JobId] = job;
            }

            string outcome = Outcome.Applied;
            string? error = null;
            if (job is null)
            {
                (outcome, error) = (Outcome.Rejected, "Auftrag nicht gefunden.");
            }
            else
            {
                try { await ApplyAsync(db, job, cmd, ct); }
                catch (Exception ex) when (ex is DomainException or JsonException or FormatException or ArgumentException)
                {
                    (outcome, error) = (Outcome.Rejected, ex is DomainException ? ex.Message : "Ungültige Daten im Befehl.");
                    log.LogInformation("Rejected {Type} {Command} for job {Job}: {Error}", cmd.Type, cmd.Id, cmd.JobId, error);
                }
            }

            if (job is not null)
            {
                db.ProcessedCommands.Add(new ProcessedCommand
                {
                    Id = cmd.Id, JobId = job.Id, Type = cmd.Type, Outcome = outcome, Error = error, ProcessedAt = clock.GetUtcNow(),
                });
            }
            await db.SaveChangesAsync(ct);
            results.Add(new CommandResult(cmd.Id, outcome, error));
        }
        return new PushResponse(results);
    }

    private static async Task ApplyAsync(AppDb db, Job job, SyncCommand cmd, CancellationToken ct)
    {
        switch (cmd.Type)
        {
            case "startWork": job.StartWork(cmd.At); break;
            case "stopWork": job.StopWork(cmd.At); break;
            case "complete": job.Complete(cmd.At); break;
            case "setNotes": job.SetNotes(Read<NotesPayload>(cmd).Text); break;
            case "addMaterial":
                var add = Read<AddMaterialPayload>(cmd);
                var article = await db.Articles.FindAsync([add.ArticleNumber], ct)
                    ?? throw new DomainException($"Artikel {add.ArticleNumber} ist unbekannt.");
                job.AddMaterial(add.LineId, article, add.Quantity);
                break;
            case "removeMaterial": job.RemoveMaterial(Read<RemoveMaterialPayload>(cmd).LineId); break;
            case "addPhoto":
                var photo = Read<AddPhotoPayload>(cmd);
                job.AddPhoto(photo.PhotoId, photo.ContentType, Convert.FromBase64String(photo.Base64), photo.Caption, cmd.At);
                break;
            case "sign":
                var sig = Read<SignPayload>(cmd);
                job.Sign(sig.SignedBy, Convert.FromBase64String(sig.PngBase64), cmd.At);
                break;
            default: throw new DomainException($"Unbekannter Befehl „{cmd.Type}“.");
        }
    }

    private static T Read<T>(SyncCommand cmd) =>
        cmd.Payload is { } p ? p.Deserialize<T>(Json) ?? throw new JsonException() : throw new JsonException();
}
