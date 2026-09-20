using Application.Abstractions.FileSystem;
using Application.Abstractions.Database;
using Application.Abstractions.Imdb;
using Application.Configuration;
using Application.Normalization;
using Business.Services;
using Host.Configuration;
using Infrastructure.FileSystem;
using Infrastructure.Imdb;
using Infrastructure.Database;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<MediaLibraryOptions>()
    .Bind(builder.Configuration.GetSection("MediaLibrary"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<MediaLibraryOptions>, MediaLibraryOptionsValidator>();
builder.Services.Configure<FileManagerOptions>(
    builder.Configuration.GetSection("FileManager"));
builder.Services.Configure<OmdbOptions>(
    builder.Configuration.GetSection(OmdbOptions.SectionName));

builder.Services.AddSingleton(static services =>
{
    var options = services.GetRequiredService<IOptions<MediaLibraryOptions>>().Value;
    return new MediaLibraryNormalizationRequest(
        options.Locations,
        options.MediaTypes
            .Select(mediaType => new MediaTypeNormalizationRequest(
                mediaType.Id,
                mediaType.Name,
                mediaType.Subdirectory,
                mediaType.OutputDirectory,
                mediaType.Enabled))
            .ToArray());
});
builder.Services.AddSingleton(static services =>
{
    var options = services.GetRequiredService<IOptions<FileManagerOptions>>().Value;
    var extensions = options.AllowedVideoExtensions is { Length: > 0 }
        ? options.AllowedVideoExtensions
        : FileManagerSettings.DefaultAllowedVideoExtensions;

    return new FileManagerSettings(extensions);
});
builder.Services.AddSingleton(static services =>
{
    var options = services.GetRequiredService<IOptions<OmdbOptions>>().Value;
    return new OmdbClientSettings(
        options.BaseUrl,
        options.ApiKey,
        options.TimeoutSeconds);
});

builder.Services.AddHttpClient<IImdbClient, ImdbClient>((services, client) =>
{
    var settings = services.GetRequiredService<OmdbClientSettings>();
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
});
builder.Logging.AddFilter(
    $"System.Net.Http.HttpClient.{typeof(IImdbClient).FullName}",
    LogLevel.None);
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlConnectionFactory(builder.Configuration.GetConnectionString("MediaNormalizer")));
builder.Services.AddScoped<INormalizationRunsRepository, NormalizationRunsRepository>();
builder.Services.AddScoped<IMediaTitlesRepository, MediaTitlesRepository>();
builder.Services.AddScoped<IMediaFilesRepository, MediaFilesRepository>();
builder.Services.AddScoped<INormalizationFileResultsRepository, NormalizationFileResultsRepository>();
builder.Services.AddScoped<INormalizationDeletedDirectoriesRepository, NormalizationDeletedDirectoriesRepository>();
builder.Services.AddSingleton<IFileManager, FileManager>();
builder.Services.AddTransient<MediaTypeHandler>();
builder.Services.AddTransient<INormalizationService, NormalizationService>();
builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "MediaNormalizer API v1");
});
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

public partial class Program
{
}
