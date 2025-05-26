using Microsoft.EntityFrameworkCore;

namespace Webhook.Navferty.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RequestModel> Requests { get; set; }
    public DbSet<ResponseModel> Responses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResponseModel>()
            .HasIndex(r => new { r.TenantId, r.Path })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}

public sealed class RequestModel
{
    public required Guid Id { get; set; }
    public required Guid TenantId { get; set; }
    public required string Path { get; set; }
    public required string IpAddress { get; set; }
    public required string Method { get; set; }
    public required string QueryString { get; set; }
    public required string Headers { get; set; }
    public required string Body { get; set; }
    public required RequestContentType ContentType { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}

public sealed class RequestDto
{
    public required Guid Id { get; set; }
    public required string Path { get; set; }
    public required string Method { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}

public sealed class ResponseModel
{
    public Guid Id { get; set; }
    public required Guid TenantId { get; init; }
    public required string Path { get; init; }

    public required string Body { get; set; }
    public required ResponseContentType ContentType { get; set; }
    public required int ResponseCode { get; set; }
    public required DateTimeOffset LastModifiedAt { get; set; }
}

public enum RequestContentType
{
    None,
    Json,
    Form,
    Text,
    Html
}

public enum ResponseContentType
{
    Json,
    Text,
    Html
}