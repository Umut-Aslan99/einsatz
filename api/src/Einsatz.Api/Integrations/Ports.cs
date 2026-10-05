using Einsatz.Domain;

namespace Einsatz.Api.Integrations;

// Ports to the customer's existing systems. The app never talks to an ERP or DMS directly,
// so swapping the file-based demo adapters for a real ERP/DMS API touches only these classes.

public interface IErpConnector
{
    Task<IntegrationRecord> ExportAsync(Job job, DateTimeOffset at, CancellationToken ct);
}

public interface IDocumentArchive
{
    Task<IntegrationRecord> ArchiveAsync(Job job, byte[] pdf, DateTimeOffset at, CancellationToken ct);
}

public sealed class IntegrationOptions
{
    public string ErpImportPath { get; set; } = "/data/erp-import";
    public string ArchivePath { get; set; } = "/data/dms";
}
