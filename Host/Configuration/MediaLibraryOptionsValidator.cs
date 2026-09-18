using Domain.Enums;
using Microsoft.Extensions.Options;

namespace Host.Configuration;

public sealed class MediaLibraryOptionsValidator : IValidateOptions<MediaLibraryOptions>
{
    public ValidateOptionsResult Validate(string? name, MediaLibraryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = options.MediaTypes
            .Select((mediaType, index) => (mediaType, index))
            .Where(static entry => entry.mediaType.Enabled
                && entry.mediaType.Id == MediaType.Tv
                && (string.IsNullOrWhiteSpace(entry.mediaType.OutputDirectory)
                    || !Path.IsPathRooted(entry.mediaType.OutputDirectory)))
            .Select(static entry =>
                $"MediaLibrary:MediaTypes:{entry.index}:OutputDirectory must be a non-empty absolute path for an enabled TV media type.")
            .ToArray();

        return failures.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
