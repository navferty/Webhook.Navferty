using Microsoft.EntityFrameworkCore;

namespace Webhook.Navferty;

public sealed class ResponseRepository(AppDbContext context)
{
    public async Task ConfigureResponse(Guid tenantId, string path, string jsonBody)
    {
        if (string.IsNullOrWhiteSpace(jsonBody))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(jsonBody));

        var normalizedPath = path.Trim().ToLowerInvariant();

        var existingResponse = await FindResponse(tenantId, normalizedPath);
        if (existingResponse is not null)
        {
            existingResponse.Body = jsonBody;
            existingResponse.LastModifiedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
            return;
        }

        var response = new ResponseModel
        {
            TenantId = tenantId,
            Path = normalizedPath,
            Body = jsonBody,
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
                LastModifiedAt = r.LastModifiedAt,
            })
            .Where(x => x.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<ResponseModel?> FindResponse(Guid tenantId, string path)
    {
        var pathNormalized = path.Trim().ToLowerInvariant();
        return await context.Responses
            .Select(r => new ResponseModel
            {
                Id = r.Id,
                TenantId = r.TenantId,
                Path = r.Path,
                Body = r.Body,
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
}
