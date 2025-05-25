using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Webhook.Navferty.Data;

public interface IRequestRepository
{
    Task SaveRequest(HttpRequest request, Guid tenantId, CancellationToken cancellationToken);

    Task<RequestModel?> GetRequest(Guid tenantId, Guid requestId);

    Task<IEnumerable<RequestDto>> GetRequests(Guid tenantId, DateTimeOffset from, DateTimeOffset to);
}

public sealed class RequestRepository(AppDbContext appDbContext)
    : IRequestRepository
{
    public async Task SaveRequest(HttpRequest request, Guid tenantId, CancellationToken cancellationToken)
    {
        string body;
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var sb = new StringBuilder();

            foreach (var (key, value) in form)
            {
                sb.Append(key);
                sb.Append('=');
                sb.Append(value);
                sb.Append("\r\n");
            }

            foreach (var file in form.Files)
            {
                sb.Append(file.Name);
                sb.Append("=file:[");
                sb.Append(file.FileName);
                sb.Append(",size:");
                sb.Append(file.Length);
                sb.Append(",type:");
                sb.Append(file.ContentType);
                sb.Append(']');
                sb.Append("\r\n");
            }

            if (sb.Length > 0)
                sb.Length -= 2;

            body = sb.ToString();
        }
        else if (request.ContentType?.StartsWith("application/json") == true)
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync(cancellationToken);
            body = JsonSerializer.Serialize(JsonDocument.Parse(json), new JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            using var reader = new StreamReader(request.Body);
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var headers = new StringBuilder();
        foreach (var (key, value) in request.Headers)
        {
            headers.Append(key);
            headers.Append(": ");
            headers.Append(value);
            headers.Append("\r\n");
        }

        if (headers.Length > 0)
            headers.Length -= 2;

        var ip = request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? request.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? request.HttpContext.Connection.LocalIpAddress?.ToString()
            ?? throw new InvalidOperationException("Cannot determine IP address");
        var requestModel = new RequestModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IpAddress = ip,
            Path = request.Path,
            Method = request.Method,
            QueryString = request.QueryString.ToString(),
            Headers = headers.ToString(),
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow
        };

        appDbContext.Requests.Add(requestModel);
        await appDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RequestModel?> GetRequest(Guid tenantId, Guid requestId)
    {
        return await appDbContext.Requests
            .Select(r => new RequestModel
            {
                Id = r.Id,
                IpAddress = r.IpAddress,
                TenantId = r.TenantId,
                Path = r.Path,
                Method = r.Method,
                QueryString = r.QueryString,
                Headers = r.Headers,
                Body = r.Body,
                CreatedAt = r.CreatedAt
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requestId && x.TenantId == tenantId);
    }

    public async Task<IEnumerable<RequestDto>> GetRequests(Guid tenantId, DateTimeOffset from, DateTimeOffset to)
    {
        return await appDbContext.Requests
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to && r.TenantId == tenantId)
            .Select(r => new RequestDto
            {
                Id = r.Id,
                Path = r.Path,
                Method = r.Method,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
    }
}
