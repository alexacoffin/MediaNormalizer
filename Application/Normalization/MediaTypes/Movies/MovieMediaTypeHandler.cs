namespace Application.Normalization.MediaTypes.Movies;

public sealed class MovieMediaTypeHandler : IMediaTypeHandler
{
    public MovieMediaTypeHandler(string[] locations)
    {
    }

    public Task Normalize() => Task.CompletedTask;
}
