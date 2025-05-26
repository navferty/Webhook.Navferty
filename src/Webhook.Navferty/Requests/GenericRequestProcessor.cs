using Microsoft.AspNetCore.Mvc;
using System.Text;
using Webhook.Navferty.Data;

namespace Webhook.Navferty.Requests;

public sealed class GenericRequestProcessor(IRequestRepository repository, ResponseRepository responseRepo)
{
    public async Task<IResult> ProcessRequest(Guid tenantId, string requestPath, HttpRequest request, CancellationToken cancellationToken)
    {
        await repository.SaveRequest(request, tenantId, cancellationToken);

        var configuredResponse = await responseRepo.FindResponse(tenantId, request.Path.Value ?? "/", cancellationToken);
        if (configuredResponse is null)
            return Results.Json(new { message = "No response configured for this path" });

        var responseBody = configuredResponse.Body ?? "No response configured for this path";

        var response = new ContentResult
        {
            Content = responseBody,
            ContentType = configuredResponse.ContentType switch
            {
                ResponseContentType.Json => "application/json",
                ResponseContentType.Text => "text/plain",
                ResponseContentType.Html => "text/html",
                _ => "application/octet-stream"
            },
            StatusCode = configuredResponse.ResponseCode > 100 && configuredResponse.ResponseCode < 600
                ? configuredResponse.ResponseCode
                : 200,
        };

        return Results.Content(
            content: response.Content,
            contentType: response.ContentType,
            contentEncoding: Encoding.UTF8,
            statusCode: response.StatusCode.Value);
    }
}
