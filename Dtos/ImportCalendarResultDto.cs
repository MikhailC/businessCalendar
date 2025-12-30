using System.Text.Json.Serialization;

namespace BusinessCalendarAPI.Dtos;

public sealed record ImportCalendarResultDto
{
    [JsonPropertyName("totalItems")]
    public required int TotalItems { get; init; }

    [JsonPropertyName("inserted")]
    public required int Inserted { get; init; }

    [JsonPropertyName("updated")]
    public required int Updated { get; init; }

    [JsonPropertyName("errors")]
    public required IReadOnlyList<string> Errors { get; init; }
}


