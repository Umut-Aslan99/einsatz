using System.Linq.Expressions;
using Einsatz.Domain;

namespace Einsatz.Api.Jobs;

// Read models shared by the REST sync API (technician app) and GraphQL (office).
// Binary data never travels inside them; photos, signatures and files are fetched by URL.

public sealed record CustomerDto(string Number, string Name, string Street, string City);
public sealed record MachineDto(string Name, string SerialNumber);
public sealed record TimeEntryDto(Guid Id, DateTimeOffset Start, DateTimeOffset? End);
public sealed record PhotoDto(Guid Id, string? Caption, DateTimeOffset TakenAt)
{
    public string Url => $"/api/files/photos/{Id}";
}
public sealed record SignatureDto(string SignedBy, DateTimeOffset SignedAt);
public sealed record IntegrationDto(Guid Id, string Target, string FileName, DateTimeOffset CreatedAt)
{
    public string Url => $"/api/files/integrations/{Id}";
}
public sealed record ArticleDto(string Number, string Name, string Unit, decimal Price);

public sealed record MaterialDto(Guid Id, string ArticleNumber, string ArticleName, string Unit, decimal Quantity, decimal UnitPrice)
{
    public decimal Total => Math.Round(Quantity * UnitPrice, 2);
}

public sealed record JobDto
{
    public Guid Id { get; init; }
    public required string Number { get; init; }
    public DateOnly ScheduledFor { get; init; }
    public required string Task { get; init; }
    public JobStatus Status { get; init; }
    public string? Notes { get; init; }
    public required CustomerDto Customer { get; init; }
    public required MachineDto Machine { get; init; }
    public required string TechnicianName { get; init; }
    public required List<TimeEntryDto> TimeEntries { get; init; }
    public required List<MaterialDto> Materials { get; init; }
    public required List<PhotoDto> Photos { get; init; }
    public SignatureDto? Signature { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public string? ApprovedBy { get; init; }
    public string? ReturnReason { get; init; }
    public List<IntegrationDto> Integrations { get; init; } = [];

    public double TotalMinutes => TimeEntries.Where(t => t.End is not null).Sum(t => Math.Round((t.End!.Value - t.Start).TotalMinutes));
    public decimal MaterialTotal => Materials.Sum(m => m.Total);
    public string ReportUrl => $"/api/files/jobs/{Id}/report.pdf";

    /// <summary>EF-translatable projection: selects exactly these columns, never photo or signature bytes.</summary>
    public static readonly Expression<Func<Job, JobDto>> Projection = j => new JobDto
    {
        Id = j.Id,
        Number = j.Number,
        ScheduledFor = j.ScheduledFor,
        Task = j.Task,
        Status = j.Status,
        Notes = j.Notes,
        Customer = new CustomerDto(j.Customer.Number, j.Customer.Name, j.Customer.Street, j.Customer.City),
        Machine = new MachineDto(j.Machine.Name, j.Machine.SerialNumber),
        TechnicianName = j.Technician.Name,
        TimeEntries = j.TimeEntries.OrderBy(t => t.Start).Select(t => new TimeEntryDto(t.Id, t.Start, t.End)).ToList(),
        Materials = j.Materials.Select(m => new MaterialDto(m.Id, m.ArticleNumber, m.ArticleName, m.Unit, m.Quantity, m.UnitPrice)).ToList(),
        Photos = j.Photos.OrderBy(p => p.TakenAt).Select(p => new PhotoDto(p.Id, p.Caption, p.TakenAt)).ToList(),
        Signature = j.Signature == null ? null : new SignatureDto(j.Signature.SignedBy, j.Signature.SignedAt),
        CompletedAt = j.CompletedAt,
        ApprovedAt = j.ApprovedAt,
        ApprovedBy = j.ApprovedBy,
        ReturnReason = j.ReturnReason,
    };
}
