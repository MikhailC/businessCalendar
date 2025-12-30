using System.Text.Json.Serialization;

namespace BusinessCalendarAPI.Dtos;

public sealed record CalendarDayResponseDto
{
    [JsonPropertyName("date")]
    public required string Date { get; init; } // yyyy-MM-dd

    [JsonPropertyName("dayName")]
    public required string DayName { get; init; }

    [JsonPropertyName("dayType")]
    public required string DayType { get; init; }

    [JsonPropertyName("day")]
    public required int Day { get; init; }

    [JsonPropertyName("month")]
    public required int Month { get; init; }

    [JsonPropertyName("year")]
    public required int Year { get; init; }

    // Требование: "число и строка" — отдаём оба формата.
    [JsonPropertyName("fromhour")]
    public required int FromHour { get; init; }

    [JsonPropertyName("tohour")]
    public required int ToHour { get; init; }

    [JsonPropertyName("fromtime")]
    public required string FromTime { get; init; } // HH:mm

    [JsonPropertyName("totime")]
    public required string ToTime { get; init; } // HH:mm
}


