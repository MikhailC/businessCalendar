namespace BusinessCalendarAPI.Services;

/// <summary>
/// Cache for calendar "РФ" for a single year (loaded at startup, updated on POST import).
/// </summary>
public sealed class RfCurrentYearCache
{
    private readonly object _lock = new();
    private int _year;
    private Dictionary<DateOnly, CalendarImportParser.ParsedItem> _byDate = new();

    public int Year
    {
        get { lock (_lock) return _year; }
    }

    public void Replace(int year, IEnumerable<CalendarImportParser.ParsedItem> itemsForYear)
    {
        lock (_lock)
        {
            _year = year;
            _byDate = itemsForYear
                .GroupBy(x => x.Date)
                .ToDictionary(g => g.Key, g => g.Last());
        }
    }

    public bool TryGet(DateOnly date, out CalendarImportParser.ParsedItem item)
    {
        lock (_lock)
        {
            return _byDate.TryGetValue(date, out item!);
        }
    }
}



