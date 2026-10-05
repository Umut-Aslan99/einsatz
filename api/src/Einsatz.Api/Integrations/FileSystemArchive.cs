using System.Text.Json;
using Einsatz.Domain;
using Microsoft.Extensions.Options;

namespace Einsatz.Api.Integrations;

/// <summary>
/// Files the signed report PDF together with an index sidecar (customer, job, date, document type),
/// the way a DMS filing profile would. A real DMS adapter would send the same metadata via its API.
/// </summary>
public sealed class FileSystemArchive(IOptions<IntegrationOptions> options) : IDocumentArchive
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task<IntegrationRecord> ArchiveAsync(Job job, byte[] pdf, DateTimeOffset at, CancellationToken ct)
    {
        var folder = System.IO.Path.Combine(options.Value.ArchivePath, job.Customer.Number, job.ScheduledFor.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var fileName = $"Servicebericht_{job.Number}.pdf";
        await File.WriteAllBytesAsync(System.IO.Path.Combine(folder, fileName), pdf, ct);

        var index = new
        {
            Dokumenttyp = "Servicebericht",
            Kundennummer = job.Customer.Number,
            Kunde = job.Customer.Name,
            Auftragsnummer = job.Number,
            Belegdatum = job.ScheduledFor,
            Unterzeichnet = job.Signature?.SignedBy,
            Archiviert = at,
        };
        await File.WriteAllTextAsync(System.IO.Path.Combine(folder, fileName + ".json"), JsonSerializer.Serialize(index, Json), ct);

        return new IntegrationRecord { JobId = job.Id, Target = "DMS", FileName = fileName, ContentType = "application/pdf", Content = pdf, CreatedAt = at };
    }
}
