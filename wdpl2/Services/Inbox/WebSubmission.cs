using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wdpl2.Services.Inbox;

/// <summary>
/// One row from <c>/api/admin/pending.php</c> — a pending submission
/// posted by a captain (or other public form) on wdpl.uk.
/// </summary>
public sealed class WebSubmission
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("season_id")]
    public string? SeasonId { get; set; }

    [JsonPropertyName("reference_id")]
    public string? ReferenceId { get; set; }

    /// <summary>Raw JSON payload as posted by the form.</summary>
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }

    [JsonPropertyName("submitter")]
    public string? Submitter { get; set; }

    [JsonPropertyName("received_utc")]
    public string? ReceivedUtc { get; set; }

    /// <summary>Pretty-printed payload for display in the UI.</summary>
    [JsonIgnore]
    public string PayloadPretty
    {
        get
        {
            try
            {
                return JsonSerializer.Serialize(Payload, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return Payload.GetRawText();
            }
        }
    }

    [JsonIgnore]
    public string Summary =>
        $"#{Id} · {Type}" +
        (string.IsNullOrWhiteSpace(ReferenceId) ? "" : $" · ref {ReferenceId}") +
        (string.IsNullOrWhiteSpace(Submitter)    ? "" : $" · by {Submitter}") +
        (string.IsNullOrWhiteSpace(ReceivedUtc)  ? "" : $" · {ReceivedUtc} UTC");
}

internal sealed class PendingResponse
{
    [JsonPropertyName("items")]
    public List<WebSubmission> Items { get; set; } = new();
}
