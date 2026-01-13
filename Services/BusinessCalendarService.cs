using BusinessCalendarAPI.Dtos;

namespace BusinessCalendarAPI.Services;

public sealed class BusinessCalendarService
{
    private readonly BusinessCalendarFileStore _store;
    private readonly CalendarImportParser _parser;
    private readonly RfCurrentYearCache _rfCache;

    public BusinessCalendarService(
        BusinessCalendarFileStore store,
        CalendarImportParser parser,
        RfCurrentYearCache rfCache)
    {
        _store = store;
        _parser = parser;
        _rfCache = rfCache;
    }

    public async Task<CalendarImportParser.ParsedItem?> GetDayAsync(string calendar, DateOnly date, CancellationToken ct)
    {
        // Fast path: cache only for РФ current year.
        if (calendar == "РФ" && date.Year == _rfCache.Year && _rfCache.TryGet(date, out var cached))
            return cached;

        var index = await LoadIndexAsync(ct);
        return index.TryGetValue((calendar, date), out var item) ? item : null;
    }

    public async Task<Dictionary<DateOnly, CalendarImportParser.ParsedItem>> GetPeriodAsync(
        string calendar,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var result = new Dictionary<DateOnly, CalendarImportParser.ParsedItem>();

        // If period intersects cached year for РФ, fill from cache first
        if (calendar == "РФ" && _rfCache.Year != 0)
        {
            var y = _rfCache.Year;
            var start = from.Year == y ? from : new DateOnly(y, 1, 1);
            var end = to.Year == y ? to : new DateOnly(y, 12, 31);
            if (start <= end)
            {
                for (var d = start; d <= end; d = d.AddDays(1))
                {
                    if (_rfCache.TryGet(d, out var cached))
                        result[d] = cached;
                }
            }
        }

        // Fill remaining from file (single read per request)
        var index = await LoadIndexAsync(ct);
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (result.ContainsKey(d))
                continue;
            if (index.TryGetValue((calendar, d), out var item))
                result[d] = item;
        }

        return result;
    }

    public CalendarDayResponseDto ToResponseDto(
        DateOnly date,
        CalendarImportParser.ParsedItem? itemOrNull,
        TimeOnly? queryStart,
        TimeOnly? queryEnd)
    {
        var hasRecord = itemOrNull is not null;
        var dayType = hasRecord ? itemOrNull!.DayType : "Обычный";

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

    private async Task<Dictionary<(string calendar, DateOnly date), CalendarImportParser.ParsedItem>> LoadIndexAsync(CancellationToken ct)
    {
        var bytes = await _store.TryReadAllBytesAsync(ct);
        if (bytes is null)
            return new Dictionary<(string calendar, DateOnly date), CalendarImportParser.ParsedItem>();

        using var ms = new MemoryStream(bytes);
        var parsed = await _parser.ParseAsync(ms, ct);
        if (parsed.Errors.Count > 0)
        {
            // If file is broken, behave as empty (API remains available).
            return new Dictionary<(string calendar, DateOnly date), CalendarImportParser.ParsedItem>();
        }

        var dict = new Dictionary<(string calendar, DateOnly date), CalendarImportParser.ParsedItem>();
        foreach (var item in parsed.Items)
            dict[(item.Calendar, item.Date)] = item; // last wins

        return dict;
    }
}


