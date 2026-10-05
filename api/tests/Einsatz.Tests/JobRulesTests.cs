using Einsatz.Domain;

namespace Einsatz.Tests;

public class JobRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 8, 0, 0, TimeSpan.FromHours(2));
    private static readonly Article Oil = new() { Number = "A-10010", Name = "Hydrauliköl", Unit = "l", Price = 6.90m };

    private static Job NewJob() => new() { Number = "A-260001", Task = "Wartung" };

    private static Job ReadyToComplete()
    {
        var job = NewJob();
        job.StartWork(T0);
        job.StopWork(T0.AddMinutes(95));
        job.Sign("M. Kunde", [1, 2, 3], T0.AddMinutes(96));
        return job;
    }

    [Fact]
    public void Starting_work_moves_job_in_progress_and_tracks_minutes()
    {
        var job = NewJob();
        job.StartWork(T0);
        job.StopWork(T0.AddMinutes(90));
        Assert.Equal(JobStatus.InProgress, job.Status);
        Assert.Equal(90, job.TotalMinutes);
    }

    [Fact]
    public void Cannot_start_twice_or_stop_before_start()
    {
        var job = NewJob();
        job.StartWork(T0);
        Assert.Throws<DomainException>(() => job.StartWork(T0.AddMinutes(1)));
        Assert.Throws<DomainException>(() => job.StopWork(T0.AddMinutes(-5)));
    }

    [Fact]
    public void Completing_requires_signature_and_stopped_clock()
    {
        var job = NewJob();
        job.StartWork(T0);
        var running = Assert.Throws<DomainException>(() => job.Complete(T0));
        Assert.Contains("laufende Arbeitszeit", running.Message);

        job.StopWork(T0.AddMinutes(30));
        var unsigned = Assert.Throws<DomainException>(() => job.Complete(T0));
        Assert.Contains("Unterschrift", unsigned.Message);
    }

    [Fact]
    public void Completed_report_is_frozen()
    {
        var job = ReadyToComplete();
        job.Complete(T0.AddHours(2));
        Assert.Throws<DomainException>(() => job.SetNotes("nachträglich"));
        Assert.Throws<DomainException>(() => job.AddMaterial(Guid.NewGuid(), Oil, 1));
    }

    [Fact]
    public void Material_line_ids_make_retries_harmless()
    {
        var job = NewJob();
        var line = Guid.NewGuid();
        job.AddMaterial(line, Oil, 2);
        job.AddMaterial(line, Oil, 2);
        var only = Assert.Single(job.Materials);
        Assert.Equal(13.80m, only.Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Material_quantity_must_be_positive(decimal qty) =>
        Assert.Throws<DomainException>(() => NewJob().AddMaterial(Guid.NewGuid(), Oil, qty));

    [Fact]
    public void Photo_limits_are_enforced()
    {
        var job = NewJob();
        Assert.Throws<DomainException>(() => job.AddPhoto(Guid.NewGuid(), "application/pdf", [1], null, T0));
        Assert.Throws<DomainException>(() => job.AddPhoto(Guid.NewGuid(), "image/jpeg", new byte[Job.MaxPhotoBytes + 1], null, T0));
        for (var i = 0; i < Job.MaxPhotos; i++) job.AddPhoto(Guid.NewGuid(), "image/jpeg", [1], null, T0);
        Assert.Throws<DomainException>(() => job.AddPhoto(Guid.NewGuid(), "image/jpeg", [1], null, T0));
    }

    [Fact]
    public void Office_workflow_approve_then_export()
    {
        var job = ReadyToComplete();
        Assert.Throws<DomainException>(() => job.Approve("Petra", T0));
        job.Complete(T0.AddHours(2));
        job.Approve("Petra", T0.AddHours(3));
        job.MarkExported();
        Assert.Equal(JobStatus.Exported, job.Status);
        Assert.Throws<DomainException>(() => job.Approve("Petra", T0));
    }

    [Fact]
    public void Returning_a_report_requires_a_new_signature()
    {
        var job = ReadyToComplete();
        job.Complete(T0.AddHours(2));
        job.ReturnToTechnician("Materialliste unvollständig");
        Assert.Equal(JobStatus.InProgress, job.Status);
        Assert.Null(job.Signature);
        Assert.Equal("Materialliste unvollständig", job.ReturnReason);
        Assert.Throws<DomainException>(() => job.Complete(T0.AddHours(4)));
    }
}
