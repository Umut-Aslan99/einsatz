using Einsatz.Api.Auth;
using Einsatz.Api.Data;
using Einsatz.Api.Reports;
using Einsatz.Domain;
using Microsoft.EntityFrameworkCore;

namespace Einsatz.Api.Files;

/// <summary>Binary content (photos, signatures, PDFs, export files), with per-user access checks.</summary>
public static class FileEndpoints
{
    public static void MapFiles(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/files").WithTags("Files").RequireAuthorization();

        g.MapGet("/photos/{id:guid}", async (Guid id, AppDb db, CurrentUser me, CancellationToken ct) =>
        {
            var photo = await (from j in db.Jobs
                               from p in j.Photos
                               where p.Id == id && (me.IsIn(Role.Office) || j.TechnicianId == me.Id)
                               select new { p.ContentType, p.Data }).SingleOrDefaultAsync(ct);
            return photo is null ? Results.NotFound() : Results.File(photo.Data, photo.ContentType);
        });

        g.MapGet("/jobs/{id:guid}/signature", async (Guid id, AppDb db, CurrentUser me, CancellationToken ct) =>
        {
            var png = await db.Jobs.Where(j => j.Id == id && (me.IsIn(Role.Office) || j.TechnicianId == me.Id))
                .Select(j => j.Signature == null ? null : j.Signature.Png).SingleOrDefaultAsync(ct);
            return png is null ? Results.NotFound() : Results.File(png, "image/png");
        });

        g.MapGet("/jobs/{id:guid}/report.pdf", async (Guid id, AppDb db, ReportPdf pdf, CurrentUser me, CancellationToken ct) =>
        {
            var archived = await db.Integrations.Where(i => i.JobId == id && i.Target == "DMS")
                .OrderByDescending(i => i.CreatedAt).Select(i => i.Content).FirstOrDefaultAsync(ct);
            if (archived is not null && me.IsIn(Role.Office)) return Results.File(archived, "application/pdf");

            var job = await db.Jobs.Full().AsNoTracking()
                .SingleOrDefaultAsync(j => j.Id == id && (me.IsIn(Role.Office) || j.TechnicianId == me.Id), ct);
            return job is null ? Results.NotFound() : Results.File(pdf.Render(job), "application/pdf", $"Servicebericht_{job.Number}.pdf");
        });

        g.MapGet("/integrations/{id:guid}", async (Guid id, AppDb db, CancellationToken ct) =>
        {
            var file = await db.Integrations.Where(i => i.Id == id)
                .Select(i => new { i.Content, i.ContentType, i.FileName }).SingleOrDefaultAsync(ct);
            return file is null ? Results.NotFound() : Results.File(file.Content, file.ContentType, file.FileName);
        }).RequireAuthorization("Office");
    }
}
