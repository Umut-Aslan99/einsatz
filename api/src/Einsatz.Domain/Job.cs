namespace Einsatz.Domain;

/// <summary>
/// A service job (Auftrag) and its field report. All state changes go through methods
/// that enforce the business rules; the offline client replays the same operations as commands.
/// </summary>
public sealed class Job
{
    public const int MaxPhotos = 8;
    public const int MaxPhotoBytes = 3 * 1024 * 1024;
    public const int MaxNotesLength = 4000;

    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Number { get; init; }
    public Guid CustomerId { get; init; }
    public Customer Customer { get; init; } = null!;
    public Guid MachineId { get; init; }
    public Machine Machine { get; init; } = null!;
    public Guid TechnicianId { get; init; }
    public User Technician { get; init; } = null!;
    public DateOnly ScheduledFor { get; init; }
    public required string Task { get; init; }

    public JobStatus Status { get; private set; } = JobStatus.Planned;
    public string? Notes { get; private set; }
    public Signature? Signature { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovedBy { get; private set; }
    public string? ReturnReason { get; private set; }

    public List<TimeEntry> TimeEntries { get; init; } = [];
    public List<MaterialLine> Materials { get; init; } = [];
    public List<Photo> Photos { get; init; } = [];

    /// <summary>Optimistic concurrency token (mapped to PostgreSQL xmin).</summary>
    public uint Version { get; private set; }

    public double TotalMinutes => TimeEntries.Sum(t => t.Minutes);
    public decimal MaterialTotal => Materials.Sum(m => m.Total);
    public bool IsEditable => Status is JobStatus.Planned or JobStatus.InProgress;
    private TimeEntry? OpenEntry => TimeEntries.FirstOrDefault(t => t.End is null);

    public void StartWork(DateTimeOffset at)
    {
        EnsureEditable();
        if (OpenEntry is not null) throw new DomainException("Die Arbeitszeit läuft bereits.");
        if (TimeEntries.Any(t => t.End > at)) throw new DomainException("Der Start liegt vor dem Ende eines vorherigen Zeitblocks.");
        TimeEntries.Add(new TimeEntry { Start = at });
        Status = JobStatus.InProgress;
    }

    public void StopWork(DateTimeOffset at)
    {
        EnsureEditable();
        var open = OpenEntry ?? throw new DomainException("Es läuft keine Arbeitszeit.");
        if (at < open.Start) throw new DomainException("Das Ende liegt vor dem Start.");
        open.End = at;
    }

    public void SetNotes(string? text)
    {
        EnsureEditable();
        if (text?.Length > MaxNotesLength) throw new DomainException($"Notizen dürfen höchstens {MaxNotesLength} Zeichen haben.");
        Notes = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Touch();
    }

    public void AddMaterial(Guid lineId, Article article, decimal quantity)
    {
        EnsureEditable();
        if (quantity <= 0) throw new DomainException("Die Menge muss größer als 0 sein.");
        if (Materials.Any(m => m.Id == lineId)) return;
        Materials.Add(new MaterialLine
        {
            Id = lineId, ArticleNumber = article.Number, ArticleName = article.Name,
            Unit = article.Unit, Quantity = quantity, UnitPrice = article.Price,
        });
        Touch();
    }

    public void RemoveMaterial(Guid lineId)
    {
        EnsureEditable();
        Materials.RemoveAll(m => m.Id == lineId);
    }

    public void AddPhoto(Guid photoId, string contentType, byte[] data, string? caption, DateTimeOffset at)
    {
        EnsureEditable();
        if (Photos.Any(p => p.Id == photoId)) return;
        if (Photos.Count >= MaxPhotos) throw new DomainException($"Höchstens {MaxPhotos} Fotos pro Auftrag.");
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp")) throw new DomainException("Nur JPEG-, PNG- oder WebP-Bilder.");
        if (data.Length is 0 or > MaxPhotoBytes) throw new DomainException("Das Foto ist leer oder größer als 3 MB.");
        Photos.Add(new Photo { Id = photoId, ContentType = contentType, Data = data, Caption = caption?.Trim(), TakenAt = at });
        Touch();
    }

    public void Sign(string signedBy, byte[] png, DateTimeOffset at)
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(signedBy)) throw new DomainException("Bitte den Namen des Unterzeichners angeben.");
        if (png.Length == 0) throw new DomainException("Die Unterschrift ist leer.");
        Signature = new Signature { SignedBy = signedBy.Trim(), Png = png, SignedAt = at };
        Touch();
    }

    public void Complete(DateTimeOffset at)
    {
        EnsureEditable();
        if (OpenEntry is not null) throw new DomainException("Bitte zuerst die laufende Arbeitszeit stoppen.");
        if (TimeEntries.Count == 0) throw new DomainException("Es wurde keine Arbeitszeit erfasst.");
        if (Signature is null) throw new DomainException("Die Unterschrift des Kunden fehlt.");
        Status = JobStatus.Completed;
        CompletedAt = at;
        ReturnReason = null;
    }

    public void Approve(string by, DateTimeOffset at)
    {
        if (Status != JobStatus.Completed) throw new DomainException("Nur abgeschlossene Berichte können freigegeben werden.");
        Status = JobStatus.Approved;
        ApprovedBy = by;
        ApprovedAt = at;
    }

    public void MarkExported()
    {
        if (Status != JobStatus.Approved) throw new DomainException("Nur freigegebene Berichte können exportiert werden.");
        Status = JobStatus.Exported;
    }

    /// <summary>Office sends the report back. Content will change, so the customer has to sign again.</summary>
    public void ReturnToTechnician(string reason)
    {
        if (Status != JobStatus.Completed) throw new DomainException("Nur abgeschlossene Berichte können zurückgegeben werden.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Bitte einen Grund angeben.");
        Status = JobStatus.InProgress;
        ReturnReason = reason.Trim();
        Signature = null;
        CompletedAt = null;
    }

    private void EnsureEditable()
    {
        if (!IsEditable) throw new DomainException($"Auftrag {Number} ist abgeschlossen und kann nicht mehr geändert werden.");
    }

    /// <summary>Any report edit counts as starting the job.</summary>
    private void Touch()
    {
        if (Status == JobStatus.Planned) Status = JobStatus.InProgress;
    }
}
