using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Einsatz.Tests;

/// <summary>End-to-end API behaviour against a real database: auth, offline sync, office workflow, integrations.</summary>
public class ApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> LoginAs(string login)
    {
        var client = api.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { login, password = "demo" }, Ct);
        res.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonNode> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync(Ct);
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        return JsonNode.Parse(body)!;
    }

    private static object Cmd(Guid jobId, string type, object? payload = null, Guid? id = null) =>
        new { id = id ?? Guid.NewGuid(), jobId, type, at = DateTimeOffset.UtcNow, payload };

    private async Task<(HttpClient Client, Guid JobId)> TechnicianWithPlannedJob()
    {
        var tech = await LoginAs("jonas");
        var pull = await Json(await tech.GetAsync("/api/sync/pull", Ct));
        var job = pull["jobs"]!.AsArray().First(j => (string)j!["status"]! == "Planned")!;
        return (tech, (Guid)job["id"]!);
    }

    private static async Task<JsonArray> Push(HttpClient client, params object[] commands) =>
        (await Json(await client.PostAsJsonAsync("/api/sync/push", new { commands }, Ct)))["results"]!.AsArray();

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var res = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { login = "jonas", password = "nope" }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Form_posts_are_refused_as_csrf_defence()
    {
        var res = await api.CreateClient().PostAsync("/api/auth/login",
            new StringContent("login=jonas&password=demo", Encoding.UTF8, "application/x-www-form-urlencoded"), Ct);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, res.StatusCode);
    }

    [Fact]
    public async Task Technicians_only_see_their_own_jobs_and_no_office_api()
    {
        var tech = await LoginAs("jonas");
        var pull = await Json(await tech.GetAsync("/api/sync/pull", Ct));
        Assert.All(pull["jobs"]!.AsArray(), j => Assert.Equal("Jonas Terhaar", (string)j!["technicianName"]!));
        Assert.NotEmpty(pull["articles"]!.AsArray());

        var gql = await tech.PostAsJsonAsync("/graphql", new { query = "{ jobs { number } }" }, Ct);
        var body = await gql.Content.ReadAsStringAsync(Ct);
        Assert.Contains("AUTH_NOT_AUTHORIZED", body);
    }

    [Fact]
    public async Task Replaying_an_offline_batch_is_idempotent()
    {
        var (tech, jobId) = await TechnicianWithPlannedJob();
        var batch = new[]
        {
            Cmd(jobId, "startWork"),
            Cmd(jobId, "addMaterial", new { lineId = Guid.NewGuid(), articleNumber = "A-10010", quantity = 4 }),
            Cmd(jobId, "setNotes", new { text = "Leckage behoben" }),
        };

        var first = await Push(tech, batch);
        Assert.All(first, r => Assert.Equal("applied", (string)r!["outcome"]!));

        // Connection dropped before the device saw the answer: it sends the same batch again.
        var second = await Push(tech, batch);
        Assert.All(second, r => Assert.Equal("duplicate", (string)r!["outcome"]!));

        var pull = await Json(await tech.GetAsync("/api/sync/pull", Ct));
        var job = pull["jobs"]!.AsArray().Single(j => (Guid)j!["id"]! == jobId)!;
        Assert.Single(job["materials"]!.AsArray());
        Assert.Single(job["timeEntries"]!.AsArray());
        Assert.Equal("Leckage behoben", (string)job["notes"]!);
    }

    [Fact]
    public async Task A_rejected_command_does_not_block_the_rest_of_the_batch()
    {
        var (tech, jobId) = await TechnicianWithPlannedJob();
        var results = await Push(tech,
            Cmd(jobId, "complete"),
            Cmd(jobId, "setNotes", new { text = "trotzdem gespeichert" }),
            Cmd(jobId, "explode"));

        Assert.Equal("rejected", (string)results[0]!["outcome"]!);
        Assert.Equal("applied", (string)results[1]!["outcome"]!);
        Assert.Equal("rejected", (string)results[2]!["outcome"]!);
        Assert.Contains("Unbekannter Befehl", (string)results[2]!["error"]!);
    }

    [Fact]
    public async Task Field_report_flows_from_technician_through_office_into_erp_and_dms()
    {
        var (tech, jobId) = await TechnicianWithPlannedJob();
        var signature = Convert.ToBase64String(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "signature.png"), Ct));

        var results = await Push(tech,
            Cmd(jobId, "startWork"),
            Cmd(jobId, "addMaterial", new { lineId = Guid.NewGuid(), articleNumber = "A-10020", quantity = 1 }),
            Cmd(jobId, "addPhoto", new { photoId = Guid.NewGuid(), contentType = "image/png", base64 = signature, caption = "Zylinder" }),
            Cmd(jobId, "stopWork"),
            Cmd(jobId, "sign", new { signedBy = "M. Kunde", pngBase64 = signature }),
            Cmd(jobId, "complete"));
        Assert.All(results, r => Assert.Equal("applied", (string)r!["outcome"]!));

        var office = await LoginAs("petra");
        var approve = await Json(await office.PostAsJsonAsync("/graphql", new
        {
            query = "mutation($id: UUID!) { approveJob(id: $id) { status integrations { target fileName url } } }",
            variables = new { id = jobId },
        }, Ct));
        Assert.True(approve["errors"] is null, approve["errors"]?.ToJsonString());
        var approved = approve["data"]!["approveJob"]!;
        Assert.Equal("EXPORTED", (string)approved["status"]!);

        var files = approved["integrations"]!.AsArray();
        var erp = files.Single(f => (string)f!["target"]! == "ERP")!;
        var xml = await office.GetStringAsync((string)erp["url"]!, Ct);
        Assert.Contains("<Artikelnummer>A-10020</Artikelnummer>", xml);
        Assert.Contains("<Artikelnummer>SRV-STD</Artikelnummer>", xml);
        Assert.True(File.Exists(Path.Combine(api.DataDir, "erp", (string)erp["fileName"]!)));

        var pdf = await office.GetByteArrayAsync($"/api/files/jobs/{jobId}/report.pdf", Ct);
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);

        // A photo that cannot be decoded must not block the next approval.
        var (_, otherJob) = await TechnicianWithPlannedJob();
        Assert.All(await Push(tech,
            Cmd(otherJob, "startWork"),
            Cmd(otherJob, "addPhoto", new { photoId = Guid.NewGuid(), contentType = "image/jpeg", base64 = Convert.ToBase64String([0xFF, 0xD8, 1, 2]), caption = "kaputt" }),
            Cmd(otherJob, "stopWork"),
            Cmd(otherJob, "sign", new { signedBy = "M. Kunde", pngBase64 = signature }),
            Cmd(otherJob, "complete")), r => Assert.Equal("applied", (string)r!["outcome"]!));
        var second = await Json(await office.PostAsJsonAsync("/graphql", new
        {
            query = "mutation($id: UUID!) { approveJob(id: $id) { status } }",
            variables = new { id = otherJob },
        }, Ct));
        Assert.True(second["errors"] is null, second["errors"]?.ToJsonString());

        // The technician's device sees the final state; further edits are refused.
        var late = await Push(tech, Cmd(jobId, "setNotes", new { text = "zu spät" }));
        Assert.Equal("rejected", (string)late[0]!["outcome"]!);
    }

    [Fact]
    public async Task Approving_twice_reports_the_business_rule()
    {
        var office = await LoginAs("petra");
        var list = await Json(await office.PostAsJsonAsync("/graphql", new { query = "{ jobs(status: EXPORTED) { id } }" }, Ct));
        var id = (string)list["data"]!["jobs"]![0]!["id"]!;

        var res = await Json(await office.PostAsJsonAsync("/graphql", new
        {
            query = "mutation($id: UUID!) { approveJob(id: $id) { status } }",
            variables = new { id },
        }, Ct));
        var error = res["errors"]![0]!;
        Assert.Equal("BUSINESS_RULE", (string)error["extensions"]!["code"]!);
        Assert.Contains("abgeschlossene", (string)error["message"]!);
    }
}
