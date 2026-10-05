namespace Einsatz.Domain;

public enum Role { Technician, Office }

/// <summary>
/// Lifecycle of a service job. Technicians move it to <see cref="Completed"/>;
/// only the office can approve, which triggers the ERP export and DMS archiving.
/// </summary>
public enum JobStatus { Planned, InProgress, Completed, Approved, Exported }

/// <summary>A business rule was violated. The message is shown to the user as-is (German).</summary>
public sealed class DomainException(string message) : Exception(message);

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Login { get; init; }
    public required string Name { get; init; }
    public Role Role { get; init; }
    public string PasswordHash { get; set; } = "";
}

public sealed class Customer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    /// <summary>Customer number as known to the ERP.</summary>
    public required string Number { get; init; }
    public required string Name { get; init; }
    public required string Street { get; init; }
    public required string City { get; init; }
}

public sealed class Machine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public required string Name { get; init; }
    public required string SerialNumber { get; init; }
}

/// <summary>Article master data, mirrored from the ERP.</summary>
public sealed class Article
{
    public required string Number { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public decimal Price { get; init; }
}

public sealed class TimeEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset? End { get; set; }
    public double Minutes => End is { } e ? Math.Round((e - Start).TotalMinutes) : 0;
}

/// <summary>Material used on site. Name and price are snapshotted so later ERP changes don't rewrite history.</summary>
public sealed class MaterialLine
{
    public Guid Id { get; init; }
    public required string ArticleNumber { get; init; }
    public required string ArticleName { get; init; }
    public required string Unit { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Total => Math.Round(Quantity * UnitPrice, 2);
}

public sealed class Photo
{
    public Guid Id { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Data { get; init; }
    public string? Caption { get; init; }
    public DateTimeOffset TakenAt { get; init; }
}

public sealed class Signature
{
    public required string SignedBy { get; init; }
    public required byte[] Png { get; init; }
    public DateTimeOffset SignedAt { get; init; }
}

/// <summary>Result of handing a job to an external system (ERP import file, DMS archive document).</summary>
public sealed class IntegrationRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JobId { get; init; }
    public required string Target { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Remembers every offline command ever applied, so retries after a dropped connection are harmless.</summary>
public sealed class ProcessedCommand
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public required string Type { get; init; }
    public required string Outcome { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset ProcessedAt { get; init; }
}
