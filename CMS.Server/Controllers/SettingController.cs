using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingController(SettingService settingService) : ControllerBase
{
    [HttpPut("password")]
    public IActionResult UpdatePasswords([FromBody] Dictionary<string, JsonElement> payload)
    {
        try
        {
            var passwords = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, element) in payload)
            {
                if (element.TryGetInt32(out int val))
                    passwords[key] = val;
                else
                    return BadRequest(new { error = $"Invalid value for '{key}'. Must be an integer." });
            }

            if (passwords.Count == 0)
                return BadRequest(new { error = "No valid department passwords supplied." });

            var result = settingService.ChangeDepartmentPasswords(passwords);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to write passwords to PLC.", detail = ex.Message });
        }
    }

    [HttpGet("plc-signals")]
    public IActionResult GetPlcSignals([FromQuery] int? machineId)
    {
        // If machineId provided: single machine. Otherwise: all 26.
        if (machineId.HasValue)
        {
            if (machineId.Value < 1 || machineId.Value > 26)
                return BadRequest(new { error = "machineId must be between 1 and 26." });

            try { return Ok(settingService.ReadSubPlcSignals(machineId.Value)); }
            catch (Exception ex) { return StatusCode(503, new { error = ex.Message }); }
        }

        return Ok(settingService.ReadAllPlcSignals());
    }

    [HttpGet("sap")]
    public async Task<ActionResult> SAP()
    {
        var data = await settingService.LoadSAP();
        return Ok(data);
    }

    [HttpPut("sap")]
    public async Task<ActionResult> UpdateSAP([FromBody] Dictionary<string, JsonElement> sapItem)
    {
        if (sapItem == null || !sapItem.ContainsKey("id_type") || !sapItem.ContainsKey("mould"))
            return BadRequest(new { error = "Missing required fields" });

        try
        {
            await settingService.UpdateSAP(sapItem);
            return Ok(new { message = "Successfully updated SAP" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("sap")]
    public async Task<ActionResult> DeleteSAP([FromQuery] int id_type, [FromQuery] int mould)
    {
        try
        {
            await settingService.DeleteSAP(id_type, mould);

            return Ok(new { message = "Deleted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("sap")]
    public async Task<ActionResult> InsertSAP([FromBody] Dictionary<string, JsonElement> sapItem)
    {
        if (sapItem == null || !sapItem.ContainsKey("id_type") || !sapItem.ContainsKey("mould"))
            return BadRequest(new { error = "Missing required fields" });

        try
        {
            await settingService.InsertSAP(sapItem);
            return Ok(new { message = "Successfully updated SAP" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("sap/import")]
    public async Task<ActionResult> ImportSAP([FromBody] List<Dictionary<string, JsonElement>> sapItems)
    {
        if (sapItems == null || sapItems.Count == 0)
            return BadRequest(new { error = "No data provided" });

        try
        {
            await settingService.ImportSAP(sapItems);
            return Ok(new { message = $"Successfully imported {sapItems.Count} SAP records" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}