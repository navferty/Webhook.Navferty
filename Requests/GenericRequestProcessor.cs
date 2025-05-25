using Microsoft.AspNetCore.Mvc;
using System.Text;
using Webhook.Navferty.Data;

namespace Webhook.Navferty.Requests;

public sealed class GenericRequestProcessor(IRequestRepository repository, ResponseRepository responseRepo)
{
    public async Task<IResult> ProcessRequest(Guid tenantId, string requestPath, HttpRequest request, CancellationToken cancellationToken)
    {
        await repository.SaveRequest(request, tenantId, cancellationToken);

        var response = await responseRepo.FindResponse(tenantId, request.Path.Value ?? "/", cancellationToken);
        if (response is null)
            return Results.Json(new { message = "No response configured for this path" });

        var responseBody = response.Body ?? "No response configured for this path";

        var responseMessage = new ContentResult
        {
            Content = responseBody,
            ContentType = response.ContentType switch
            {
                ResponseContentType.Json => "application/json",
                ResponseContentType.Text => "text/plain",
                ResponseContentType.Html => "text/html",
                _ => "application/octet-stream"
            },
            StatusCode = 200
        };

        return Results.Content(
            content: responseMessage.Content,
            contentType: responseMessage.ContentType,
            contentEncoding: Encoding.UTF8,
            statusCode: responseMessage.StatusCode.Value);
    }
}
