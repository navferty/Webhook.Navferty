using System.Text;

namespace Webhook.Navferty.Requests;

public static class HttpRequestHelpers
{
    // currently no way to get raw request body in ASP.NET Core:
    // Kestrel API to retrieve raw http request bytes #6210
    // https://github.com/dotnet/aspnetcore/issues/6210

    public static string ReconstructRawHttpRequest(this HttpRequest request)
    {
        var sb = new StringBuilder();

        // Request line: METHOD path?query HTTP/version
        var pathAndQuery = request.PathBase + request.Path + request.QueryString;
        sb.Append($"{request.Method} {pathAndQuery} HTTP/{request.Protocol.Replace("HTTP/", "")}\r\n");

        // Headers
        foreach (var header in request.Headers)
        {
            foreach (var value in header.Value)
            {
                sb.Append($"{header.Key}: {value}\r\n");
            }
        }

        sb.Append("\r\n"); // End of headers

        return sb.ToString();
    }
}
