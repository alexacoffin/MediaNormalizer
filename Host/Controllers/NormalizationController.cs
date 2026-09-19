using Application.Normalization;
using Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace Host.Controllers;

[ApiController]
[Route("normalize")]
public sealed class NormalizationController(INormalizationService normalizationService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(NormalizationResult), StatusCodes.Status200OK)]
    public Task<NormalizationResult> NormalizeMediaFiles() =>
        normalizationService.NormalizeMediaFiles();
}
