using Einsatz.Api.Auth;
using Einsatz.Api.Demo;
using Einsatz.Api.Jobs;
using Einsatz.Api.Office;
using Einsatz.Domain;
using HotChocolate.Authorization;

namespace Einsatz.Api.GraphQL;

/// <summary>GraphQL API for the office (Innendienst): flexible reads, explicit workflow mutations.</summary>
[Authorize(Roles = ["Office"])]
public sealed class Query
{
    public Task<List<JobDto>> GetJobs(JobStatus? status, [Service] JobReads reads, CancellationToken ct) =>
        reads.ListAsync(q => status is null ? q : q.Where(j => j.Status == status), ct);

    public Task<JobDto?> GetJob(Guid id, [Service] JobReads reads, CancellationToken ct) => reads.GetAsync(id, ct);

    public Task<List<ArticleDto>> GetArticles([Service] JobReads reads, CancellationToken ct) => reads.ArticlesAsync(ct);
}

[Authorize(Roles = ["Office"])]
public sealed class Mutation
{
    /// <summary>Approve a completed report: creates the PDF, exports to the ERP and archives in the DMS.</summary>
    public Task<JobDto> ApproveJob(Guid id, [Service] ApprovalService approvals, [Service] CurrentUser me, CancellationToken ct) =>
        approvals.ApproveAsync(id, me.Name, ct);

    /// <summary>Send a report back to the technician with a reason.</summary>
    public Task<JobDto> ReturnJob(Guid id, string reason, [Service] ApprovalService approvals, CancellationToken ct) =>
        approvals.ReturnAsync(id, reason, ct);

    /// <summary>Restore the demo data set.</summary>
    public async Task<bool> ResetDemo([Service] DemoSeeder seeder, CancellationToken ct)
    {
        await seeder.ResetAsync(ct);
        return true;
    }
}

/// <summary>Business rule violations reach the client with their German message instead of a generic error.</summary>
public static class DomainErrors
{
    public static IError Translate(IError error) => error.Exception is DomainException ex
        ? ErrorBuilder.FromError(error).SetMessage(ex.Message).SetCode("BUSINESS_RULE").SetException(null).Build()
        : error;
}
