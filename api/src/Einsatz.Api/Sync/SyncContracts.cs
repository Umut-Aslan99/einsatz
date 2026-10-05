using System.Text.Json;
using Einsatz.Api.Jobs;

namespace Einsatz.Api.Sync;

/// <summary>
/// One change recorded on the device, possibly while offline. <see cref="Id"/> is generated
/// on the device, which makes re-sending the same command safe.
/// </summary>
public sealed record SyncCommand(Guid Id, Guid JobId, string Type, DateTimeOffset At, JsonElement? Payload);

public sealed record PushRequest(List<SyncCommand> Commands);

public static class Outcome
{
    public const string Applied = "applied";
    public const string Duplicate = "duplicate";
    public const string Rejected = "rejected";
}

public sealed record CommandResult(Guid Id, string Outcome, string? Error = null);
public sealed record PushResponse(List<CommandResult> Results);
public sealed record PullResponse(DateTimeOffset ServerTime, List<JobDto> Jobs, List<ArticleDto> Articles);

// Payload shapes per command type.
public sealed record NotesPayload(string? Text);
public sealed record AddMaterialPayload(Guid LineId, string ArticleNumber, decimal Quantity);
public sealed record RemoveMaterialPayload(Guid LineId);
public sealed record AddPhotoPayload(Guid PhotoId, string ContentType, string Base64, string? Caption);
public sealed record SignPayload(string SignedBy, string PngBase64);
