using Einsatz.Api.Auth;
using Einsatz.Api.Data;
using Einsatz.Api.Office;
using Einsatz.Domain;
using Microsoft.EntityFrameworkCore;

namespace Einsatz.Api.Demo;

/// <summary>
/// Fictional demo data, always relative to "today" so the demo never looks stale.
/// Every login uses the password <c>demo</c>.
/// </summary>
public sealed class DemoSeeder(IDbContextFactory<AppDb> dbs, IServiceProvider services, TimeProvider clock, ILogger<DemoSeeder> log)
{
    public const string Password = "demo";

    public async Task SeedIfEmptyAsync(CancellationToken ct = default)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        if (!await db.Users.AnyAsync(ct)) await SeedAsync(ct);
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await using (var db = await dbs.CreateDbContextAsync(ct))
        {
            await db.Database.ExecuteSqlRawAsync(
                """TRUNCATE "ProcessedCommands", "Integrations", "Photo", "MaterialLine", "TimeEntry", "Jobs", "Machines", "Customers", "Articles", "Users" CASCADE""", ct);
        }
        await SeedAsync(ct);
        log.LogInformation("Demo data reset");
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        await using var db = await dbs.CreateDbContextAsync(ct);
        var tz = clock.LocalTimeZone;
        var today = DateOnly.FromDateTime(clock.GetLocalNow().Date);
        DateTimeOffset At(DateOnly day, int h, int m)
        {
            var local = day.ToDateTime(new TimeOnly(h, m));
            return new DateTimeOffset(local, tz.GetUtcOffset(local));
        }

        User Person(string login, string name, Role role)
        {
            var u = new User { Login = login, Name = name, Role = role };
            u.PasswordHash = AuthEndpoints.Hash(u, Password);
            return u;
        }
        var jonas = Person("jonas", "Jonas Terhaar", Role.Technician);
        var lea = Person("lea", "Lea Wissing", Role.Technician);
        var petra = Person("petra", "Petra Lammers", Role.Office);
        db.Users.AddRange(jonas, lea, petra);

        var c1 = new Customer { Number = "K10023", Name = "Aawerk Kunststofftechnik GmbH", Street = "Werkstraße 14", City = "46395 Bocholt" };
        var c2 = new Customer { Number = "K10057", Name = "Hohenhorst Landmaschinen KG", Street = "Am Kanal 3", City = "46414 Rhede" };
        var c3 = new Customer { Number = "K10102", Name = "Issel Präzisionsteile GmbH", Street = "Gewerbering 8", City = "46419 Isselburg" };
        var c4 = new Customer { Number = "K10140", Name = "Westmünsterland Druckguss AG", Street = "Hüttenweg 21", City = "46325 Borken" };
        db.Customers.AddRange(c1, c2, c3, c4);

        Machine M(Customer c, string name, string sn) => new() { CustomerId = c.Id, Name = name, SerialNumber = sn };
        var press = M(c1, "Hydraulikpresse HP-200", "HP200-2019-0412");
        var spritz = M(c1, "Spritzgießmaschine SG-350", "SG350-2021-0077");
        var comp = M(c2, "Schraubenkompressor K-75", "K75-2017-1180");
        var mill = M(c3, "CNC-Fräszentrum FZ-500", "FZ500-2020-0231");
        var crane = M(c4, "Hallenkran 5 t", "HK5-2015-0009");
        var belt = M(c4, "Förderband Linie 3", "FB-L3-2022-0003");
        db.Machines.AddRange(press, spritz, comp, mill, crane, belt);

        Article A(string no, string name, string unit, decimal price) => new() { Number = no, Name = name, Unit = unit, Price = price };
        var articles = new[]
        {
            A("A-10010", "Hydrauliköl HLP 46", "l", 6.90m),
            A("A-10020", "Dichtungssatz Zylinder Ø50", "Stk", 48.50m),
            A("A-10031", "Keilriemen SPZ 1250", "Stk", 17.80m),
            A("A-10045", "Luftfilterelement K-75", "Stk", 36.20m),
            A("A-10046", "Ölabscheider K-75", "Stk", 64.00m),
            A("A-10052", "Schütz 24 V DC", "Stk", 54.00m),
            A("A-10060", "Näherungsschalter M12 PNP", "Stk", 29.90m),
            A("A-10071", "Kugellager 6205-2RS", "Stk", 8.40m),
            A("A-10080", "Schmierfett Lithium 400 g", "Stk", 9.95m),
            A("A-10091", "Feinsicherung 10 A träge", "Stk", 1.20m),
            A("A-10110", "Hydraulikschlauch DN10, 1 m", "Stk", 32.00m),
            A("A-10125", "Reinigungstücher", "Pkg", 12.90m),
            A("A-10130", "Kühlschmierstoff-Konzentrat", "l", 11.40m),
            A("A-10140", "Not-Aus-Taster", "Stk", 41.00m),
            A("A-10150", "Zahnriemen HTD 8M-30", "Stk", 44.60m),
        };
        db.Articles.AddRange(articles);

        var seq = 140;
        Job J(User tech, Customer c, Machine m, DateOnly day, string task) => new()
        {
            Number = $"A-{day:yy}{++seq:0000}", CustomerId = c.Id, MachineId = m.Id, TechnicianId = tech.Id, ScheduledFor = day, Task = task,
        };

        var jobs = new List<Job>
        {
            J(jonas, c1, press, today, "Ölverlust am Hauptzylinder – Dichtungen prüfen und ggf. tauschen."),
            J(jonas, c2, comp, today, "Jahreswartung: Luftfilter, Ölabscheider, Keilriemen."),
            J(jonas, c4, crane, today, "UVV-Prüfung Hallenkran, Endschalter kontrollieren."),
            J(jonas, c3, mill, today.AddDays(1), "Spindel läuft laut – Lager begutachten."),
            J(lea, c4, belt, today, "Band läuft schief, Sensor an Station 2 meldet sporadisch."),
            J(lea, c1, spritz, today, "Störung Heizzone 3 – Schütz prüfen."),
        };

        // Yesterday: one report waiting for office review, one already approved and exported.
        var pending = J(lea, c3, mill, today.AddDays(-1), "Kühlschmierstoff wechseln, Späneförderer reinigen.");
        pending.StartWork(At(today.AddDays(-1), 7, 45));
        pending.AddMaterial(Guid.NewGuid(), articles[12], 18);
        pending.AddMaterial(Guid.NewGuid(), articles[11], 1);
        pending.StopWork(At(today.AddDays(-1), 9, 20));
        pending.SetNotes("KSS-Konzentration auf 6 % eingestellt. Späneförderer hat leichte Laufgeräusche – beim nächsten Termin beobachten.");
        var signature = ReadSignature();
        pending.Sign("M. Hüsing", signature, At(today.AddDays(-1), 9, 22));
        pending.Complete(At(today.AddDays(-1), 9, 23));

        var done = J(jonas, c2, comp, today.AddDays(-2), "Druckabfall im Netz – Leckagesuche.");
        done.StartWork(At(today.AddDays(-2), 13, 0));
        done.AddMaterial(Guid.NewGuid(), articles[10], 2);
        done.StopWork(At(today.AddDays(-2), 14, 35));
        done.Sign("H. Hohenhorst", signature, At(today.AddDays(-2), 14, 40));
        done.Complete(At(today.AddDays(-2), 14, 41));

        jobs.AddRange([pending, done]);
        db.Jobs.AddRange(jobs);
        await db.SaveChangesAsync(ct);

        // Exported history goes through the real approval path, so its files are genuine.
        await services.GetRequiredService<ApprovalService>().ApproveAsync(done.Id, petra.Name, ct);
        log.LogInformation("Seeded demo data: {Jobs} jobs", jobs.Count);
    }

    private static byte[] ReadSignature()
    {
        using var s = typeof(DemoSeeder).Assembly.GetManifestResourceStream("Einsatz.Api.Demo.signature.png")!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}

/// <summary>Restores the demo every night at 03:00 local time, so visitors always find a clean state.</summary>
public sealed class NightlyReset(IServiceScopeFactory scopes, TimeProvider clock, IConfiguration config, ILogger<NightlyReset> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Demo:NightlyReset", true)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetLocalNow();
            var next = now.Date.AddDays(now.Hour >= 3 ? 1 : 0).AddHours(3);
            await Task.Delay(next - now.DateTime, clock, stoppingToken);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DemoSeeder>().ResetAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Nightly demo reset failed");
            }
        }
    }
}
