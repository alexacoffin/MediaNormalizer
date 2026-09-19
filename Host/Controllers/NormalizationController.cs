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
        return Ok(await normalizationService.NormalizeMediaFiles());
    }
}
