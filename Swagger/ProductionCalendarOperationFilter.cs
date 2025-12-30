using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BusinessCalendarAPI.Swagger;

public sealed class ProductionCalendarOperationFilter : IOperationFilter
{
    private const string ImportPath = "/api/production-calendar/import";
    private const string DayPath = "/api/production-calendar/day";
    private const string PeriodPath = "/api/production-calendar/period";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        path = "/" + path.TrimStart('/');

        if (string.Equals(path, ImportPath, StringComparison.OrdinalIgnoreCase))
        {
            ApplyImport(operation);
            return;
        }

        if (string.Equals(path, DayPath, StringComparison.OrdinalIgnoreCase))
        {
            ApplyDay(operation);
            return;
        }

        if (string.Equals(path, PeriodPath, StringComparison.OrdinalIgnoreCase))
        {
            ApplyPeriod(operation);
        }
    }

    private static void ApplyImport(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
            return;

        if (!operation.RequestBody.Content.TryGetValue("application/xml", out var mediaType))
            return;

        mediaType.Example = new OpenApiString(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Items Description=\"Данные производственного календаря\" Columns=\"Calendar,Year,DayType,Date,SwapDate\">\n" +
            "  <Item Calendar=\"РФ\" Year=\"2017\" DayType=\"Рабочий\" Date=\"20171001\" SwapDate=\"20171006\"/>\n" +
            "  <Item Calendar=\"РФ\" Year=\"2017\" DayType=\"Предпраздничный\" Date=\"20171004\" SwapDate=\"\"/>\n" +
            "  <Item Calendar=\"РФ\" Year=\"2017\" DayType=\"Праздник\" Date=\"20171005\" SwapDate=\"\"/>\n" +
            "  <Item Calendar=\"РФ\" Year=\"2017\" DayType=\"Воскресенье\" Date=\"20171006\" SwapDate=\"20171001\"/>\n" +
            "</Items>");
    }

    private static void ApplyDay(OpenApiOperation operation)
    {
        DescribeParam(operation, "calendar",
            "Код календаря (string). По умолчанию \"РФ\".",
            example: new OpenApiString("РФ"));

        DescribeParam(operation, "date",
            "Дата. Формат: yyyy-MM-dd или yyyyMMdd.",
            example: new OpenApiString("2017-10-05"));

        DescribeParam(operation, "starttime",
            "Начало рабочего дня (HH:mm). Если не задан — 09:00.",
            example: new OpenApiString("09:00"));

        DescribeParam(operation, "endtime",
            "Конец рабочего дня (HH:mm). Если не задан — 18:00.",
            example: new OpenApiString("18:00"));
    }

    private static void ApplyPeriod(OpenApiOperation operation)
    {
        DescribeParam(operation, "calendar",
            "Код календаря (string). По умолчанию \"РФ\".",
            example: new OpenApiString("РФ"));

        DescribeParam(operation, "from",
            "Дата начала периода. Формат: yyyy-MM-dd или yyyyMMdd.",
            example: new OpenApiString("2017-10-01"));

        DescribeParam(operation, "to",
            "Дата конца периода. Формат: yyyy-MM-dd или yyyyMMdd.",
            example: new OpenApiString("2017-10-10"));

        DescribeParam(operation, "starttime",
            "Начало рабочего дня (HH:mm). Если не задан — 09:00.",
            example: new OpenApiString("09:00"));

        DescribeParam(operation, "endtime",
            "Конец рабочего дня (HH:mm). Если не задан — 18:00.",
            example: new OpenApiString("18:00"));
    }

    private static void DescribeParam(OpenApiOperation operation, string name, string description, IOpenApiAny? example)
    {
        var p = operation.Parameters?.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        if (p is null)
            return;

        p.Description = description;
        if (example is not null)
            p.Example = example;
    }
}


