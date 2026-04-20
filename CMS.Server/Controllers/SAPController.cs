using CMS.server.Services;
using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SAPController : ControllerBase
{
    private readonly SAPService _sapService;

    public SAPController(SAPService sapService)
    {
        _sapService = sapService;
    }

    [HttpGet]
    public async Task<ActionResult> SAP()
    {
        var data = await _sapService.LoadSAP();
        return Ok(data);
    }

    [HttpPut]
    public async Task<ActionResult> UpdateSAP([FromBody] Dictionary<string, JsonElement> sapItem)
    {
        if (sapItem == null || !sapItem.ContainsKey("id_type") || !sapItem.ContainsKey("mould"))
            return BadRequest(new { error = "Missing required fields" });

        try
        {
            await _sapService.UpdateSAP(sapItem);
            return Ok(new { message = "Successfully updated SAP" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteSAP([FromQuery] int id_type, [FromQuery] int mould)
    {
        try
        {
            await _sapService.DeleteSAP(id_type, mould);

            return Ok(new { message = "Deleted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> InsertSAP([FromBody] Dictionary<string, JsonElement> sapItem)
    {
        if (sapItem == null || !sapItem.ContainsKey("id_type") || !sapItem.ContainsKey("mould"))
            return BadRequest(new { error = "Missing required fields" });

        try
        {
            await _sapService.InsertSAP(sapItem);
            return Ok(new { message = "Successfully updated SAP" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("ImportSAP")]
    public async Task<ActionResult> ImportSAP([FromBody] List<Dictionary<string, JsonElement>> sapItems)
    {
        if (sapItems == null || sapItems.Count == 0)
            return BadRequest(new { error = "No data provided" });

        try
        {
            await _sapService.ImportSAP(sapItems);
            return Ok(new { message = $"Successfully imported {sapItems.Count} SAP records" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

}