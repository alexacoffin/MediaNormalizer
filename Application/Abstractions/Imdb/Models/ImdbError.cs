using System.Net;

namespace Application.Abstractions.Imdb.Models;

public enum ImdbErrorKind
{
    NotFound,
    Authentication,
    RateLimit,
    Http,
    Transport,
    Timeout,
    InvalidResponse,
    Api
}

public sealed class ImdbError
{
    public ImdbError(ImdbErrorKind kind, string message, HttpStatusCode? statusCode = null)
    {
        Kind = kind;
        Message = message;
        StatusCode = statusCode;
    }

    public ImdbErrorKind Kind { get; }

    public string Message { get; }

    public HttpStatusCode? StatusCode { get; }
}
