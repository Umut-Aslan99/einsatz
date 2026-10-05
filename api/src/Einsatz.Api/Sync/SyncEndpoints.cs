using Einsatz.Api.Auth;
using Microsoft.EntityFrameworkCore;

namespace Einsatz.Api.Sync;

public static class SyncEndpoints
{
    public const int MaxBatch = 200;

    public static void MapSync(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sync").WithTags("Sync").RequireAuthorization("Technician");

        g.MapGet("/pull", (SyncService sync, CurrentUser me, CancellationToken ct) => sync.PullAsync(me.Id, ct));

        g.MapPost("/push", async (PushRequest req, SyncService sync, CurrentUser me, CancellationToken ct) =>
        {
            if (req.Commands.Count > MaxBatch) return Results.Problem($"Maximal {MaxBatch} Befehle pro Anfrage.", statusCode: 413);
            try { return Results.Ok(await sync.PushAsync(me.Id, req.Commands, ct)); }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else changed the job at the same moment; the device simply retries.
                return Results.Problem("Der Auftrag wurde gleichzeitig geändert. Bitte erneut synchronisieren.", statusCode: 409);
            }
        });
    }
}
