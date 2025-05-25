using Microsoft.EntityFrameworkCore;

namespace Webhook.Navferty;

public sealed class ResponseRepository(AppDbContext context)
{
    public async Task ConfigureResponse(Guid tenantId, string path, string body, ResponseContentType contentType)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(body));

        var normalizedPath = path.Trim().ToLowerInvariant();
        normalizedPath = TrimTenantId(tenantId, normalizedPath);

        var existingResponse = await context.Responses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Path == normalizedPath);

        if (existingResponse is not null)
        {
            existingResponse.Body = body;
            existingResponse.LastModifiedAt = DateTimeOffset.UtcNow;
            existingResponse.ContentType = contentType;
            await context.SaveChangesAsync();
            return;
        }

        var response = new ResponseModel
        {
            TenantId = tenantId,
            Path = normalizedPath,
            Body = body,
            ContentType = contentType,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };

        context.Responses.Add(response);
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyCollection<ResponseModel>> FindResponses(Guid tenantId)
    {
        return await context.Responses
            .Select(r => new ResponseModel
            {
                Id = r.Id,
                TenantId = r.TenantId,
                Path = r.Path,
                Body = r.Body,
                ContentType = r.ContentType,
                LastModifiedAt = r.LastModifiedAt,
            })
            .Where(x => x.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<ResponseModel?> FindResponse(Guid tenantId, string path)
    {
        var pathNormalized = path.Trim().ToLowerInvariant();
        pathNormalized = TrimTenantId(tenantId, pathNormalized);

        return await context.Responses
            .Select(r => new ResponseModel
            {
                Id = r.Id,
                TenantId = r.TenantId,
                Path = r.Path,
                Body = r.Body,
                ContentType = r.ContentType,
                LastModifiedAt = r.LastModifiedAt,
            })
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Path == pathNormalized);
    }

    public async Task DeleteResponse(Guid tenantId, string path)
    {
        var pathNormalized = path.Trim().ToLowerInvariant();
        var response = await context.Responses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Path == pathNormalized);
        if (response is not null)
        {
            context.Responses.Remove(response);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAllResponses(Guid tenantId)
    {
        var responses = await context.Responses.Where(x => x.TenantId == tenantId).ToListAsync();
        if (responses.Count > 0)
        {
            context.Responses.RemoveRange(responses);
            await context.SaveChangesAsync();
        }
    }

    private static string TrimTenantId(Guid tenantId, string pathNormalized)
    {
        var tenantString = tenantId.ToString();
        if (pathNormalized.StartsWith(tenantString + "/")
            || pathNormalized.StartsWith("/" + tenantString))
        {
            // Remove tenant ID prefix if present
            pathNormalized = pathNormalized.Substring(tenantString.Length + 1);
        }

        return pathNormalized;
    }
}
