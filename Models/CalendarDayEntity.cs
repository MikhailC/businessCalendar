namespace BusinessCalendarAPI.Models;

public sealed class CalendarDayEntity
{
    public int Id { get; set; }

    public string Calendar { get; set; } = "РФ";

    public DateOnly Date { get; set; }

    public int Year { get; set; }

    public string DayType { get; set; } = string.Empty;

    public DateOnly? SwapDate { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}


