using System.Globalization;

namespace BusinessCalendarAPI.Services;

public static class BusinessCalendarRules
{
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static string GetRussianDayName(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        var name = dt.ToString("dddd", RuCulture); // usually lowercase
        return RuCulture.TextInfo.ToTitleCase(name);
    }

    public static bool IsNonWorkingDayType(string? dayType)
    {
        if (string.IsNullOrWhiteSpace(dayType))
            return false;

        return dayType.Trim() switch
        {
            "Праздник" => true,
            "Суббота" => true,
            "Воскресенье" => true,
            _ => false
        };
    }

    public static bool IsPreHoliday(string? dayType)
    {
        if (string.IsNullOrWhiteSpace(dayType))
            return false;

        return string.Equals(dayType.Trim(), "Предпраздничный", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWeekendByDayOfWeek(DateOnly date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    public static (TimeOnly from, TimeOnly to) CalculateWorkInterval(
        DateOnly date,
        string effectiveDayType,
        bool hasCalendarRecordPriority,
        TimeOnly? queryStart,
        TimeOnly? queryEnd)
    {
        // Если есть запись в календаре — приоритет выше дня недели (например, воскресенье может стать рабочим).
        var isNonWorking = hasCalendarRecordPriority
            ? IsNonWorkingDayType(effectiveDayType)
            : IsWeekendByDayOfWeek(date);

        if (!hasCalendarRecordPriority && IsNonWorkingDayType(effectiveDayType))
        {
            // на всякий случай: если нет записи, но dayType почему-то уже "Праздник" и т.п. — считаем нерабочим
            isNonWorking = true;
        }

        if (isNonWorking)
            return (new TimeOnly(0, 0), new TimeOnly(0, 0));

        var start = queryStart ?? new TimeOnly(9, 0);
        var end = queryEnd ?? new TimeOnly(18, 0);

        // Пятница: на 1 час меньше конца рабочего дня.
        if (date.DayOfWeek == DayOfWeek.Friday)
            end = end.AddHours(-1);

        // Предпраздничный: ещё на 1 час меньше.
        if (IsPreHoliday(effectiveDayType))
            end = end.AddHours(-1);

        if (end < start)
            end = start;

        return (start, end);
    }
}


