using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using Webhook.Navferty;
using Webhook.Navferty.Data;
using Webhook.Navferty.Requests;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("WEBHOOK_");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<ResponseRepository>();
builder.Services.AddScoped<GenericRequestProcessor>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<RequestRateLimitingOptions>(
    builder.Configuration.GetSection(RequestRateLimitingOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

using var scope = app.Services.CreateScope();
var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
appDbContext.Database.EnsureCreated();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseForwardedHeaders();
}

app.UseStaticFiles();
app.UseRouting();

app.UseMiddleware<RequestRateLimitingMiddleware>();

app.MapRazorPages();

app.MapGet("/", async context =>
{
    await Task.CompletedTask;
    var tenantId = Guid.NewGuid();
    context.Response.Redirect("/Index?tenantId=" + tenantId);
});

app.MapGet("{tenantId:guid}/requests/{id:guid}", async (Guid tenantId, Guid id, IRequestRepository repository) =>
{
    var request = await repository.GetRequest(tenantId, id);
    return request is not null ? Results.Ok(request) : Results.NotFound();
})
.WithName("GetRequestById")
.WithOpenApi();

app.MapGet("{tenantId:guid}/requests", async (Guid tenantId, DateTimeOffset from, DateTimeOffset to, IRequestRepository repository) =>
{
    var requests = await repository.GetRequests(tenantId, from, to);
    return Results.Ok(requests);
})
.WithName("GetRequests")
.WithOpenApi();

app.MapPost("{tenantId:guid}/responses", async (Guid tenantId, [FromBody] CreateResponseDto dto, ResponseRepository responseRepo, CancellationToken ct) =>
{
    if (!CreateResponseValidator.Validate(dto, out var error))
        return Results.BadRequest(new { error });
    await responseRepo.ConfigureResponse(tenantId, dto.Path ?? "/", dto.Body, dto.ContentType, dto.ResponseCode, ct);
    return Results.Created($"/{tenantId}/responses", null);
});

app.MapDelete("{tenantId:guid}/responses", async (Guid tenantId, [FromQuery]string path, ResponseRepository responseRepo, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(path))
        return Results.BadRequest(new { error = "Path cannot be null or whitespace." });
    await responseRepo.DeleteResponse(tenantId, path, ct);
    return Results.Ok();
});

app.MapGet("{tenantId:guid}/favicon.ico", (Guid tenantId) =>
{
    return Results.File("wwwroot/favicon.ico", "image/x-icon");
});

app.Map("{tenantId:guid}", async (Guid tenantId, HttpRequest request, GenericRequestProcessor processor, CancellationToken ct) =>
{
    return await processor.ProcessRequest(tenantId, string.Empty, request, ct);
});

app.Map("{tenantId:guid}/{**catchAll}", async (Guid tenantId, string catchAll, HttpRequest request, GenericRequestProcessor processor, CancellationToken ct) =>
{
    return await processor.ProcessRequest(tenantId, catchAll, request, ct);
})
.WithName("CatchAll")
.WithOpenApi();

app.Run();
