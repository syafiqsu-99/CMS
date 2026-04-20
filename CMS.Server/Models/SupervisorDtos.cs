using System.Text.Json.Serialization;
using System.Text.Json;

namespace CMS.Server.Models;

public record ReportUpdateDto(
    [property: JsonPropertyName("production_date")] DateOnly ProductionDate,
    [property: JsonPropertyName("shift")] int Shift,
    [property: JsonPropertyName("reportList")] List<Dictionary<string, JsonElement>> ReportList
);

public record StaffDto(
    [property: JsonPropertyName("staff_id")] int? StaffId,
    [property: JsonPropertyName("staff_name")] string? StaffName,
    [property: JsonPropertyName("staff_role")] string? StaffRole
);