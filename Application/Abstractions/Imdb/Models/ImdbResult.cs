namespace Application.Abstractions.Imdb.Models;

public sealed class ImdbResult<T>
{
    private ImdbResult(T? value, ImdbError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public ImdbError? Error { get; }

    public static ImdbResult<T> Success(T value) => new(value, null);

    public static ImdbResult<T> Failure(ImdbError error) => new(default, error);
}
