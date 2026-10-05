using System.Globalization;
using Einsatz.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Einsatz.Api.Reports;

/// <summary>Renders the customer-facing service report.</summary>
public sealed class ReportPdf(TimeProvider clock)
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private const string Accent = "#E8834A";

    public byte[] Render(Job job) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(t => t.FontSize(10).FontColor("#1a1a1a"));

        page.Header().Column(c =>
        {
            c.Item().Row(r =>
            {
                r.RelativeItem().Column(h =>
                {
                    h.Item().Text("Servicebericht").FontSize(20).Bold();
                    h.Item().Text($"Auftrag {job.Number} · {job.ScheduledFor.ToString("dd.MM.yyyy", De)}").FontColor(Colors.Grey.Darken1);
                });
                r.ConstantItem(150).AlignRight().Text("Demo · Einsatz").FontColor(Colors.Grey.Medium);
            });
            c.Item().PaddingTop(8).Height(4).Width(180).Background(Accent);
        });

        page.Content().PaddingVertical(16).Column(c =>
        {
            c.Spacing(14);
            c.Item().Row(r =>
            {
                r.RelativeItem().Element(Box).Column(k =>
                {
                    k.Item().Text("Kunde").FontColor(Colors.Grey.Darken1).FontSize(8);
                    k.Item().Text(job.Customer.Name).Bold();
                    k.Item().Text(job.Customer.Street);
                    k.Item().Text(job.Customer.City);
                    k.Item().Text($"Kd.-Nr. {job.Customer.Number}").FontColor(Colors.Grey.Darken1);
                });
                r.ConstantItem(12);
                r.RelativeItem().Element(Box).Column(k =>
                {
                    k.Item().Text("Anlage").FontColor(Colors.Grey.Darken1).FontSize(8);
                    k.Item().Text(job.Machine.Name).Bold();
                    k.Item().Text($"Seriennr. {job.Machine.SerialNumber}");
                    k.Item().Text($"Techniker: {job.Technician.Name}");
                });
            });

            c.Item().Text(t => { t.Span("Auftrag: ").Bold(); t.Span(job.Task); });

            c.Item().Text("Arbeitszeit").Bold().FontSize(12);
            c.Item().Table(t =>
            {
                t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(); d.ConstantColumn(80); });
                Head(t, "Beginn", "Ende", "Minuten");
                foreach (var e in job.TimeEntries.OrderBy(e => e.Start))
                {
                    Cell(t, Local(e.Start)); Cell(t, e.End is { } end ? Local(end) : "–"); Cell(t, e.Minutes.ToString(De), right: true);
                }
                t.Cell().ColumnSpan(2).PaddingTop(4).Text("Summe").Bold();
                t.Cell().PaddingTop(4).AlignRight().Text($"{job.TotalMinutes.ToString(De)} min").Bold();
            });

            if (job.Materials.Count > 0)
            {
                c.Item().Text("Material").Bold().FontSize(12);
                c.Item().Table(t =>
                {
                    t.ColumnsDefinition(d => { d.ConstantColumn(70); d.RelativeColumn(); d.ConstantColumn(70); d.ConstantColumn(70); d.ConstantColumn(70); });
                    Head(t, "Artikel", "Bezeichnung", "Menge", "Preis", "Summe");
                    foreach (var m in job.Materials)
                    {
                        Cell(t, m.ArticleNumber); Cell(t, m.ArticleName); Cell(t, $"{m.Quantity.ToString("0.###", De)} {m.Unit}", right: true);
                        Cell(t, m.UnitPrice.ToString("C", De), right: true); Cell(t, m.Total.ToString("C", De), right: true);
                    }
                    t.Cell().ColumnSpan(4).PaddingTop(4).Text("Material gesamt").Bold();
                    t.Cell().PaddingTop(4).AlignRight().Text(job.MaterialTotal.ToString("C", De)).Bold();
                });
            }

            if (job.Notes is { } notes)
            {
                c.Item().Text("Bemerkungen").Bold().FontSize(12);
                c.Item().Element(Box).Text(notes);
            }

            if (job.Photos.Count > 0)
            {
                c.Item().Text("Fotos").Bold().FontSize(12);
                c.Item().Inlined(i =>
                {
                    i.Spacing(8);
                    foreach (var p in job.Photos) i.Item().Width(160).Column(pc =>
                    {
                        if (TryDecode(p.Data) is { } img) pc.Item().Image(img).FitWidth();
                        else pc.Item().Height(90).Background(Colors.Grey.Lighten3).AlignMiddle().AlignCenter()
                            .Text("Bild nicht lesbar").FontSize(8).FontColor(Colors.Grey.Darken1);
                        if (p.Caption is { } cap) pc.Item().Text(cap).FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            }

            if (job.Signature is { } sig)
            {
                c.Item().PaddingTop(10).Width(260).Column(s =>
                {
                    if (TryDecode(sig.Png) is { } img) s.Item().Height(70).Image(img).FitArea();
                    else s.Item().Height(70);
                    s.Item().LineHorizontal(0.75f);
                    s.Item().Text($"{sig.SignedBy}, {Local(sig.SignedAt)}").FontSize(8);
                    s.Item().Text("Unterschrift Kunde – Arbeiten ordnungsgemäß ausgeführt").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            }
        });

        page.Footer().Row(r =>
        {
            r.RelativeItem().Text("Demo-Anwendung · alle Firmen und Personen sind frei erfunden").FontSize(7).FontColor(Colors.Grey.Medium);
            r.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Medium));
                t.Span("Seite "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
            });
        });
    })).GeneratePdf();

    private string Local(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone).ToString("dd.MM.yyyy HH:mm", De);

    /// <summary>A broken upload must never block approval; it is rendered as a placeholder instead.</summary>
    private static Image? TryDecode(byte[] data)
    {
        try { return Image.FromBinaryData(data); }
        catch (Exception) { return null; } // QuestPDF throws its own exception types for undecodable data
    }

    private static IContainer Box(IContainer c) => c.Border(0.75f).BorderColor(Colors.Grey.Lighten2).Padding(10);

    private static void Head(TableDescriptor t, params string[] titles)
    {
        t.Header(h =>
        {
            foreach (var title in titles)
                h.Cell().BorderBottom(1).BorderColor(Accent).PaddingBottom(4).Text(title).Bold().FontSize(9);
        });
    }

    private static void Cell(TableDescriptor t, string text, bool right = false)
    {
        var cell = t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);
        (right ? cell.AlignRight() : cell).Text(text);
    }
}
