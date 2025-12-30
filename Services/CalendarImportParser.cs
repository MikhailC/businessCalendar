using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace BusinessCalendarAPI.Services;

public sealed class CalendarImportParser
{
    public sealed record ParsedItem(
        string Calendar,
        int Year,
        DateOnly Date,
        string DayType,
        DateOnly? SwapDate);

    public sealed record ParseResult(int TotalItems, IReadOnlyList<ParsedItem> Items, IReadOnlyList<string> Errors);

    public Task<ParseResult> ParseAsync(Stream xmlStream, CancellationToken ct)
    {
        return ParseInternalAsync(xmlStream, ct);
    }

    // оставляем синхронный вариант для тестов/ручного использования
    public ParseResult Parse(string xml)
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return ParseInternalAsync(ms, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task<ParseResult> ParseInternalAsync(Stream xmlStream, CancellationToken ct)
    {
        var errors = new List<string>();
        var items = new List<ParsedItem>();

        XDocument doc;
        try
        {
            // IMPORTANT: XmlReader respects encoding declared in XML prolog (e.g. windows-1251).
            using var reader = XmlReader.Create(xmlStream, new XmlReaderSettings { Async = true });
            doc = await XDocument.LoadAsync(reader, LoadOptions.None, ct);
        }
        catch (Exception ex)
        {
            return new ParseResult(0, Array.Empty<ParsedItem>(), new[] { $"XML parse error: {ex.Message}" });
        }

        var itemElements = doc.Root?.Name.LocalName == "Items"
            ? doc.Root.Elements().Where(e => e.Name.LocalName == "Item").ToList()
            : doc.Descendants().Where(e => e.Name.LocalName == "Item").ToList();
        var total = itemElements.Count;

        var idx = 0;
        foreach (var el in itemElements)
        {
            idx++;
            var calendarRaw = (string?)el.Attribute("Calendar");
            var yearRaw = (string?)el.Attribute("Year");
            var dayTypeRaw = (string?)el.Attribute("DayType");
            var dateRaw = (string?)el.Attribute("Date");
            var swapDateRaw = (string?)el.Attribute("SwapDate");

            var calendar = (calendarRaw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(calendar))
                errors.Add($"Item #{idx}: Calendar is required");

            if (!TryParseInt(yearRaw, out var year))
                errors.Add($"Item #{idx}: invalid Year='{yearRaw}' (expected int)");

            var dayType = (dayTypeRaw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dayType))
                errors.Add($"Item #{idx}: DayType is required");

            if (!TryParseDateOnly(dateRaw, out var date))
                errors.Add($"Item #{idx}: invalid Date='{dateRaw}' (expected yyyyMMdd)");

            DateOnly? swapDate = null;
            if (!string.IsNullOrWhiteSpace(swapDateRaw))
            {
                if (TryParseDateOnly(swapDateRaw, out var parsedSwap))
                    swapDate = parsedSwap;
                else
                    errors.Add($"Item #{idx}: invalid SwapDate='{swapDateRaw}' (expected yyyyMMdd or empty)");
            }

            // Add item only if the required pieces parsed OK
            if (!string.IsNullOrWhiteSpace(calendar) &&
                TryParseInt(yearRaw, out year) &&
                !string.IsNullOrWhiteSpace(dayType) &&
                TryParseDateOnly(dateRaw, out date))
            {
                if (date.Year != year)
                {
                    errors.Add($"Item #{idx}: Year='{year}' does not match Date='{date:yyyyMMdd}'");
                    continue;
                }

                items.Add(new ParsedItem(
                    Calendar: calendar,
                    Year: year,
                    Date: date,
                    DayType: dayType,
                    SwapDate: swapDate));
            }
        }

        return new ParseResult(total, items, errors);
    }

    private static bool TryParseInt(string? value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDateOnly(string? value, out DateOnly result)
    {
        return DateOnly.TryParseExact(
            value,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }
}


