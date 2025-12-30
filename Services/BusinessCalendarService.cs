using BusinessCalendarAPI.Data;
using BusinessCalendarAPI.Dtos;
using BusinessCalendarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BusinessCalendarAPI.Services;

public sealed class BusinessCalendarService
{
    private readonly BusinessCalendarDbContext _db;

    public BusinessCalendarService(BusinessCalendarDbContext db)
    {
        _db = db;
    }

    public async Task<CalendarDayEntity?> GetDayAsync(string calendar, DateOnly date, CancellationToken ct)
    {
        return await _db.CalendarDays
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Calendar == calendar && x.Date == date, ct);
    }

    public async Task<Dictionary<DateOnly, CalendarDayEntity>> GetPeriodAsync(
        string calendar,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var list = await _db.CalendarDays
            .AsNoTracking()
            .Where(x => x.Calendar == calendar && x.Date >= from && x.Date <= to)
            .ToListAsync(ct);

        // если есть дубликаты (теоретически не должно быть из-за unique index) — берём последний
        return list
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ImportedAtUtc).First());
    }

    public CalendarDayResponseDto ToResponseDto(
        DateOnly date,
        CalendarDayEntity? entityOrNull,
        TimeOnly? queryStart,
        TimeOnly? queryEnd)
    {
        var hasRecord = entityOrNull is not null;
        var dayType = hasRecord ? entityOrNull!.DayType : "Обычный";

        var (from, to) = BusinessCalendarRules.CalculateWorkInterval(
            date,
            effectiveDayType: dayType,
            hasCalendarRecordPriority: hasRecord,
            queryStart: queryStart,
            queryEnd: queryEnd);

        return new CalendarDayResponseDto
        {
            Date = date.ToString("yyyy-MM-dd"),
            DayName = BusinessCalendarRules.GetRussianDayName(date),
            DayType = dayType,
            Day = date.Day,
            Month = date.Month,
            Year = date.Year,
            FromHour = from.Hour,
            ToHour = to.Hour,
            FromTime = from.ToString("HH:mm"),
            ToTime = to.ToString("HH:mm"),
        };
    }
}


