using Application.Normalization;
using Business.Services;
using Host.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text.Json;
using Xunit;

namespace Application.Tests.Host.Controllers;

public sealed class NormalizationControllerTests
{
    [Fact]
    public async Task NormalizeMediaFiles_ReturnsSummariesFromServiceResult()
    {
        var normalizationService = new Mock<INormalizationService>();
        var sourcePath = Path.Combine(Path.GetTempPath(), "intake", "episode.mkv");
        var destinationPath = Path.Combine(Path.GetTempPath(), "normalized", "episode.mkv");
        var skippedPath = Path.Combine(Path.GetTempPath(), "intake", "skipped.mkv");
        var failedPath = Path.Combine(Path.GetTempPath(), "intake", "failed.mkv");
        var alreadyNormalizedPath = Path.Combine(Path.GetTempPath(), "normalized", "existing.mkv");
        var deletedDirectory = Path.Combine(Path.GetTempPath(), "intake", "Show");
        normalizationService
            .Setup(service => service.NormalizeMediaFiles())
            .ReturnsAsync(new NormalizationResult(
                [
                    new MediaFileNormalizationResult(
                        sourcePath,
                        destinationPath,
                        MediaFileNormalizationStatus.Renamed,
                        "Renamed."),
                    new MediaFileNormalizationResult(
                        skippedPath,
                        null,
                        MediaFileNormalizationStatus.Skipped,
                        "Skipped."),
                    new MediaFileNormalizationResult(
                        failedPath,
                        null,
                        MediaFileNormalizationStatus.Failed,
                        "Failed."),
                    new MediaFileNormalizationResult(
                        alreadyNormalizedPath,
                        alreadyNormalizedPath,
                        MediaFileNormalizationStatus.AlreadyNormalized,
                        "Already normalized.")
                ],
                [deletedDirectory]));
        var controller = new NormalizationController(normalizationService.Object);

        var result = await controller.NormalizeMediaFiles();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<NormalizationResult>(okResult.Value);
        Assert.Equal("completed", response.Status);
        Assert.Equal(1, response.Renamed.Count);
        Assert.Equal([destinationPath], response.Renamed.Paths);
        Assert.Equal(1, response.Skipped.Count);
        Assert.Equal([skippedPath], response.Skipped.Paths);
        Assert.Equal(1, response.Failed.Count);
        Assert.Equal([failedPath], response.Failed.Paths);
        Assert.Equal(1, response.AlreadyNormalized.Count);
        Assert.Equal([alreadyNormalizedPath], response.AlreadyNormalized.Paths);
        Assert.Equal(1, response.RemovedFromIntake.Count);
        Assert.Equal([sourcePath], response.RemovedFromIntake.Paths);
        Assert.Equal(1, response.DeletedDirectories.Count);
        Assert.Equal([deletedDirectory], response.DeletedDirectories.Paths);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("completed", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("renamed").GetProperty("count").GetInt32());
        Assert.Equal(
            destinationPath,
            document.RootElement
                .GetProperty("renamed")
                .GetProperty("paths")[0]
                .GetString());
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
