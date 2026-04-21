using System.Text.Json;

namespace CMS.Server.Models;

public record ReportUpdateDto(
    DateOnly production_date,
    int shift,
    List<Dictionary<string, JsonElement>> reportList
);

public record StaffDto(
    int staff_id,
    string? staff_name,
    string? staff_role
);