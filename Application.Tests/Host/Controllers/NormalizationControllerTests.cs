using Business.Services;
using Host.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Application.Tests.Host.Controllers;

public sealed class NormalizationControllerTests
{
    [Fact]
    public async Task NormalizeMediaFiles_CallsServiceAndReturnsCompletedStatus()
    {
        var normalizationService = new Mock<INormalizationService>();
        var controller = new NormalizationController(normalizationService.Object);

        var result = await controller.NormalizeMediaFiles();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var status = okResult.Value?
            .GetType()
            .GetProperty("status")?
            .GetValue(okResult.Value);
        Assert.Equal("completed", status);
        normalizationService.Verify(service => service.NormalizeMediaFiles(), Times.Once);
    }

    [Fact]
    public void ControllerConstruction_DoesNotInvokeNormalizationService()
    {
        var normalizationService = new Mock<INormalizationService>();

        _ = new NormalizationController(normalizationService.Object);

        normalizationService.VerifyNoOtherCalls();
    }
}
