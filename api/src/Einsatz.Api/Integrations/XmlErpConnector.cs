using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Einsatz.Domain;
using Microsoft.Extensions.Options;

namespace Einsatz.Api.Integrations;

/// <summary>
/// Writes a delivery/service voucher as XML into the ERP's import folder – the most common
/// integration path for mid-market ERP systems. Labour is booked as service article <see cref="LabourArticle"/>.
/// </summary>
public sealed class XmlErpConnector(IOptions<IntegrationOptions> options) : IErpConnector
{
    public const string LabourArticle = "SRV-STD";
    public const decimal LabourRate = 89.00m;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<IntegrationRecord> ExportAsync(Job job, DateTimeOffset at, CancellationToken ct)
    {
        var hours = Math.Round((decimal)job.TotalMinutes / 60m, 2);
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("Belegimport",
                new XAttribute("version", "1.0"),
                new XElement("Beleg",
                    new XElement("Belegart", "Servicebericht"),
                    new XElement("Auftragsnummer", job.Number),
                    new XElement("Belegdatum", job.ScheduledFor.ToString("yyyy-MM-dd", Inv)),
                    new XElement("Kundennummer", job.Customer.Number),
                    new XElement("Anlage", new XAttribute("seriennummer", job.Machine.SerialNumber), job.Machine.Name),
                    new XElement("Techniker", job.Technician.Name),
                    new XElement("FreigegebenVon", job.ApprovedBy),
                    new XElement("Positionen",
                        Position(1, LabourArticle, "Arbeitszeit Techniker", "Std", hours, LabourRate),
                        job.Materials.Select((m, i) => Position(i + 2, m.ArticleNumber, m.ArticleName, m.Unit, m.Quantity, m.UnitPrice))),
                    new XElement("Bemerkung", job.Notes ?? ""))));

        var bytes = Encoding.UTF8.GetBytes(doc.Declaration + Environment.NewLine + doc.ToString());
        var fileName = $"{job.Number}_{at:yyyyMMddHHmmss}.xml";
        Directory.CreateDirectory(options.Value.ErpImportPath);
        await File.WriteAllBytesAsync(System.IO.Path.Combine(options.Value.ErpImportPath, fileName), bytes, ct);

        return new IntegrationRecord { JobId = job.Id, Target = "ERP", FileName = fileName, ContentType = "application/xml", Content = bytes, CreatedAt = at };
    }

    private static XElement Position(int no, string article, string name, string unit, decimal qty, decimal price) =>
        new("Position", new XAttribute("nr", no),
            new XElement("Artikelnummer", article),
            new XElement("Bezeichnung", name),
            new XElement("Menge", qty.ToString("0.###", Inv)),
            new XElement("Einheit", unit),
            new XElement("Einzelpreis", price.ToString("0.00", Inv)));
}
