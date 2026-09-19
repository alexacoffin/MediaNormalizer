using Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace Host.Controllers;

[ApiController]
[Route("normalize")]
public sealed class NormalizationController(INormalizationService normalizationService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> NormalizeMediaFiles()
    {
        await normalizationService.NormalizeMediaFiles();
        return Ok(new { status = "completed" });
    }
}
