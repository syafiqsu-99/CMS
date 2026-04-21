using System.Text.Json.Serialization;
using System.Text.Json;

namespace CMS.Server.Models;

public record ReportUpdateDto(
    [property: JsonPropertyName("production_date")] DateOnly production_date,
    [property: JsonPropertyName("shift")] int shift,
    [property: JsonPropertyName("reportList")] List<Dictionary<string, JsonElement>> reportList
);

public record StaffDto(
    [property: JsonPropertyName("staff_id")] int staff_id,
    [property: JsonPropertyName("staff_name")] string? staff_name,
    [property: JsonPropertyName("staff_role")] string? staff_role
);