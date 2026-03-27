using System;
using System.Net;

namespace XianYuLauncher.Core.Exceptions;

public sealed class AiAnalysisRequestException : Exception
{
    public AiAnalysisRequestException(string message, HttpStatusCode? statusCode = null, string? responseBody = null, string? requestUri = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        RequestUri = requestUri;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ResponseBody { get; }

    public string? RequestUri { get; }
}