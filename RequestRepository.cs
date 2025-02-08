using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Webhook.Navferty;

public interface IRequestRepository
{
    Task SaveRequest(HttpRequest request, CancellationToken cancellationToken);

    Task<RequestModel> GetRequest(Guid id);

    Task<IEnumerable<RequestDto>> GetRequests(DateTimeOffset from, DateTimeOffset to);
}

public class RequestRepository(AppDbContext appDbContext)
    : IRequestRepository
{
    public async Task SaveRequest(HttpRequest request, CancellationToken cancellationToken)
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

            body = sb.ToString().TrimEnd('\n').TrimEnd('\r');
        }
        else
        {
            using var reader = new StreamReader(request.Body);
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? request.HttpContext.Connection.LocalIpAddress?.ToString()
            ?? throw new InvalidOperationException("Cannot determine IP address");
        var requestModel = new RequestModel
        {
            Id = Guid.NewGuid(),
            IpAddress = ip,
            Path = request.Path,
            Method = request.Method,
            QueryString = request.QueryString.ToString(),
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow
        };

        appDbContext.Requests.Add(requestModel);
        await appDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RequestModel> GetRequest(Guid id)
    {
        return await appDbContext.Requests
            .Select(r => new RequestModel
            {
                Id = r.Id,
                IpAddress = r.IpAddress,
                Path = r.Path,
                Method = r.Method,
                QueryString = r.QueryString,
                Body = r.Body,
                CreatedAt = r.CreatedAt
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException();
    }

    public async Task<IEnumerable<RequestDto>> GetRequests(DateTimeOffset from, DateTimeOffset to)
    {
        return await appDbContext.Requests
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
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

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RequestModel> Requests { get; set; }
}

public class RequestModel
{
    public required Guid Id { get; set; }
    public required string Path { get; set; }
    public required string IpAddress { get; set; }
    public required string Method { get; set; }
    public required string QueryString { get; set; }
    public required string Body { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}

public class RequestDto
{
    public required Guid Id { get; set; }
    public required string Path { get; set; }
    public required string Method { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}