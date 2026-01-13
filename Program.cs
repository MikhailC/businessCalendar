using BusinessCalendarAPI.Dtos;
using BusinessCalendarAPI.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI + Swagger UI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Business Calendar API",
        Version = "v1",
        Description =
            "API производственного календаря.\n\n" +
            "Основные возможности:\n" +
            "- Загрузка календаря через POST (XML)\n" +
            "- Получение дня по дате\n" +
            "- Получение календаря по периоду\n\n" +
            "Правила:\n" +
            "- calendar (string) — код календаря. По умолчанию \"РФ\".\n" +
            "- Если в загруженном календаре есть запись на дату — она имеет приоритет над днем недели.\n" +
            "- Нерабочие DayType: Праздник, Суббота, Воскресенье.\n" +
            "- starttime/endtime (HH:mm) можно задавать по отдельности, недостающий берётся по умолчанию (09:00/18:00).",
    });

    // XML-comments (из .csproj GenerateDocumentationFile)
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    c.OperationFilter<BusinessCalendarAPI.Swagger.ProductionCalendarOperationFilter>();
});

builder.Services.AddSingleton<CalendarImportParser>();
builder.Services.AddSingleton<RfCurrentYearCache>();

var calendarFilePath = builder.Configuration["BusinessCalendar:FilePath"];
if (string.IsNullOrWhiteSpace(calendarFilePath))
    calendarFilePath = Path.Combine(builder.Environment.ContentRootPath, "businesscalendar.xml");

builder.Services.AddSingleton(new BusinessCalendarFileStore(calendarFilePath));
builder.Services.AddScoped<BusinessCalendarService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Business Calendar API v1");
    c.DocumentTitle = "Business Calendar API - Swagger";
});

// In container / production we often run behind a reverse-proxy and expose HTTP only.
// Avoid breaking HTTP-only deployments with redirects to non-existent HTTPS endpoints.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Cache: "РФ" current year loaded into memory at startup
using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<BusinessCalendarFileStore>();
    var parser = scope.ServiceProvider.GetRequiredService<CalendarImportParser>();
    var rfCache = scope.ServiceProvider.GetRequiredService<RfCurrentYearCache>();

    var year = DateTime.Today.Year;
    var bytes = await store.TryReadAllBytesAsync(CancellationToken.None);
    if (bytes is not null)
    {
        using var ms = new MemoryStream(bytes);
        var parsed = await parser.ParseAsync(ms, CancellationToken.None);
        if (parsed.Errors.Count == 0)
        {
            rfCache.Replace(year, parsed.Items.Where(x => x.Calendar == "РФ" && x.Date.Year == year));
        }
    }
    else
    {
        rfCache.Replace(year, Array.Empty<CalendarImportParser.ParsedItem>());
    }
}

// POST: строгий импорт XML (ошибки возвращаем, ничего не сохраняем при наличии ошибок)
app.MapPost("/api/production-calendar/import", async (
        HttpRequest request,
        BusinessCalendarFileStore store,
        CalendarImportParser parser,
        RfCurrentYearCache rfCache,
        CancellationToken ct) =>
    {
        // Read raw bytes (to preserve original encoding/prolog when writing to file)
        using var msRaw = new MemoryStream();
        await request.Body.CopyToAsync(msRaw, ct);
        var rawBytes = msRaw.ToArray();

        using var msParse = new MemoryStream(rawBytes);
        var parsed = await parser.ParseAsync(msParse, ct);
        if (parsed.Errors.Count > 0)
        {
            return Results.BadRequest(new ImportCalendarResultDto
            {
                TotalItems = parsed.TotalItems,
                Inserted = 0,
                Updated = 0,
                Errors = parsed.Errors
            });
        }

        await store.WriteAllBytesAsync(rawBytes, ct);

        // refresh РФ cache for current year
        var year = DateTime.Today.Year;
        rfCache.Replace(year, parsed.Items.Where(x => x.Calendar == "РФ" && x.Date.Year == year));

        return Results.Ok(new ImportCalendarResultDto
        {
            TotalItems = parsed.TotalItems,
            Inserted = 0,
            Updated = 0,
            Errors = Array.Empty<string>()
        });
    })
    .WithName("ImportProductionCalendar")
    .WithTags("Production calendar")
    .WithSummary("Импорт производственного календаря (XML)")
    .WithDescription(
        "Принимает XML вида <Items><Item .../></Items>.\n\n" +
        "Строгий режим:\n" +
        "- если есть ошибки валидации хотя бы в одной строке — вернётся 400 и НИЧЕГО не сохранится\n" +
        "- если ошибок нет — файл businesscalendar.xml будет перезаписан (полная замена)\n\n" +
        "Поддерживаемые атрибуты Item:\n" +
        "- Calendar (string)\n" +
        "- Year (int, должен совпадать с годом Date)\n" +
        "- DayType (string)\n" +
        "- Date (yyyyMMdd)\n" +
        "- SwapDate (yyyyMMdd или пусто)\n\n" +
        "Пример XML:\n" +
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<Items Description=\"Данные производственного календаря\" Columns=\"Calendar,Year,DayType,Date,SwapDate\">\n" +
        "  <Item Calendar=\"РФ\" Year=\"2017\" DayType=\"Рабочий\" Date=\"20171001\" SwapDate=\"20171006\"/>\n" +
        "  <Item Calendar=\"РФ\" Year=\"2017\" DayType=\"Предпраздничный\" Date=\"20171004\" SwapDate=\"\"/>\n" +
        "  <Item Calendar=\"РФ\" Year=\"2017\" DayType=\"Праздник\" Date=\"20171005\" SwapDate=\"\"/>\n" +
        "  <Item Calendar=\"РФ\" Year=\"2017\" DayType=\"Воскресенье\" Date=\"20171006\" SwapDate=\"20171001\"/>\n" +
        "</Items>")
    .Accepts<string>("application/xml")
    .Produces<ImportCalendarResultDto>(StatusCodes.Status200OK)
    .Produces<ImportCalendarResultDto>(StatusCodes.Status400BadRequest)
    ;

// GET: получение по дате
app.MapGet("/api/production-calendar/day", async (
        string? calendar,
        string? date,
        string? starttime,
        string? endtime,
        BusinessCalendarService svc,
        CancellationToken ct) =>
    {
        var cal = string.IsNullOrWhiteSpace(calendar) ? "РФ" : calendar.Trim();

        if (!QueryParsing.TryParseDate(date, out var d))
            return Results.BadRequest(new ErrorResponseDto { Error = "Invalid 'date'. Supported formats: yyyy-MM-dd or yyyyMMdd." });

        TimeOnly? qs = null;
        TimeOnly? qe = null;

        if (!string.IsNullOrWhiteSpace(starttime))
        {
            if (!QueryParsing.TryParseTime(starttime, out var parsedStart))
                return Results.BadRequest(new ErrorResponseDto { Error = "Invalid 'starttime'. Supported formats: HH:mm." });
            qs = parsedStart;
        }

        if (!string.IsNullOrWhiteSpace(endtime))
        {
            if (!QueryParsing.TryParseTime(endtime, out var parsedEnd))
                return Results.BadRequest(new ErrorResponseDto { Error = "Invalid 'endtime'. Supported formats: HH:mm." });
            qe = parsedEnd;
        }

        // Проверка на корректность диапазона с учётом дефолтов
        var startToValidate = qs ?? new TimeOnly(9, 0);
        var endToValidate = qe ?? new TimeOnly(18, 0);
        if (endToValidate <= startToValidate)
            return Results.BadRequest(new ErrorResponseDto { Error = "'endtime' must be greater than 'starttime'." });

        var entity = await svc.GetDayAsync(cal, d, ct);
        var dto = svc.ToResponseDto(d, entity, qs, qe);
        return Results.Ok(dto);
    })
    .WithName("GetProductionCalendarDay")
    .WithTags("Production calendar")
    .WithSummary("Получить день по дате")
    .WithDescription(
        "Возвращает один объект календарного дня.\n\n" +
        "Параметры:\n" +
        "- calendar (string, optional): по умолчанию \"РФ\"\n" +
        "- date (required): yyyy-MM-dd или yyyyMMdd\n" +
        "- starttime/endtime (optional): HH:mm (можно задавать по отдельности)\n\n" +
        "Правила времени:\n" +
        "- default: 09:00–18:00, пятница 09:00–17:00\n" +
        "- предпраздничный: -1 час от конца\n" +
        "- праздники/сб/вс: 00:00–00:00\n" +
        "- если в календаре есть запись на дату — она главнее дня недели")
    .Produces<CalendarDayResponseDto>(StatusCodes.Status200OK)
    .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest)
    ;

// GET: получение по периоду (включительно)
app.MapGet("/api/production-calendar/period", async (
        string? calendar,
        string? from,
        string? to,
        string? starttime,
        string? endtime,
        BusinessCalendarService svc,
        CancellationToken ct) =>
    {
        var cal = string.IsNullOrWhiteSpace(calendar) ? "РФ" : calendar.Trim();

        if (!QueryParsing.TryParseDate(from, out var d1))
            return Results.BadRequest(new ErrorResponseDto { Error = "Invalid 'from'. Supported formats: yyyy-MM-dd or yyyyMMdd." });
        if (!QueryParsing.TryParseDate(to, out var d2))
            return Results.BadRequest(new ErrorResponseDto { Error = "Invalid 'to'. Supported formats: yyyy-MM-dd or yyyyMMdd." });
        if (d2 < d1)
            return Results.BadRequest(new ErrorResponseDto { Error = "'to' must be greater than or equal to 'from'." });

        TimeOnly? qs = null;
        TimeOnly? qe = null;

        if (!string.IsNullOrWhiteSpace(starttime))
        {
            if (!QueryParsing.TryParseTime(starttime, out var parsedStart))
                return Results.BadRequest(new ErrorResponseDto { Error = "Invalid 'starttime'. Supported formats: HH:mm." });
            qs = parsedStart;
        }

        if (!string.IsNullOrWhiteSpace(endtime))
        {
            if (!QueryParsing.TryParseTime(endtime, out var parsedEnd))
                return Results.BadRequest(new ErrorResponseDto { Error = "Invalid 'endtime'. Supported formats: HH:mm." });
            qe = parsedEnd;
        }

        // Проверка на корректность диапазона с учётом дефолтов
        var startToValidate = qs ?? new TimeOnly(9, 0);
        var endToValidate = qe ?? new TimeOnly(18, 0);
        if (endToValidate <= startToValidate)
            return Results.BadRequest(new ErrorResponseDto { Error = "'endtime' must be greater than 'starttime'." });

        var map = await svc.GetPeriodAsync(cal, d1, d2, ct);

        var result = new List<CalendarDayResponseDto>();
        for (var day = d1; day <= d2; day = day.AddDays(1))
        {
            map.TryGetValue(day, out var entity);
            result.Add(svc.ToResponseDto(day, entity, qs, qe));
        }

        return Results.Ok(result);
    })
    .WithName("GetProductionCalendarPeriod")
    .WithTags("Production calendar")
    .WithSummary("Получить календарь по периоду")
    .WithDescription(
        "Возвращает массив объектов по каждому дню в диапазоне [from..to] (включительно).\n\n" +
        "Если на дату нет записи в календаре — dayType будет \"Обычный\".\n" +
        "starttime/endtime применяются ко всем дням периода по тем же правилам, что и /day.")
    .Produces<List<CalendarDayResponseDto>>(StatusCodes.Status200OK)
    .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest)
    ;

app.Run();
