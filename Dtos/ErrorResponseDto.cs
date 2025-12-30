using System.Text.Json.Serialization;

namespace BusinessCalendarAPI.Dtos;

public sealed record ErrorResponseDto
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }
}


