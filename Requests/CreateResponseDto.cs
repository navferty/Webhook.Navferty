using System.Text.Json;
using Webhook.Navferty.Data;

namespace Webhook.Navferty.Requests;

public record CreateResponseDto(string Path, string Body, ResponseContentType ContentType);

public class CreateResponseValidator
{
    public static bool Validate(CreateResponseDto dto, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(dto.Path))
            error = "Path cannot be null or whitespace.";
        else if (!dto.Path.StartsWith('/'))
            error = "Path should start with a slash.";
        else if (string.IsNullOrWhiteSpace(dto.Body))
            error = "Body cannot be null or whitespace.";
        else if (!Enum.IsDefined(dto.ContentType))
            error = "Invalid content type specified.";
        else
        {
            switch (dto.ContentType)
            {
                case ResponseContentType.Json:
                    try
                    {
                        _ = JsonDocument.Parse(dto.Body);
                    }
                    catch (JsonException ex)
                    {
                        error = $"Invalid JSON body: {ex.Message}";
                    }
                    break;
                case ResponseContentType.Text:
                case ResponseContentType.Html:
                    // No specific validation for text or HTML
                    break;
                default:
                    error = "Unsupported content type.";
                    break;
            }
        }

        return error is null;
    }
}
