using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Webhook.Navferty.Data;
using Webhook.Navferty.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("WEBHOOK_");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<ResponseRepository>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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

app.UseStaticFiles();
app.UseRouting();

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

app.MapPost("{tenantId:guid}/responses", async (Guid tenantId, [FromBody] CreateResponseDto dto, ResponseRepository responseRepo, HttpRequest request) =>
{
    if (!CreateResponseValidator.Validate(dto, out var error))
        return Results.BadRequest(new { error });
    await responseRepo.ConfigureResponse(tenantId, dto.Path ?? "/", dto.Body, dto.ContentType);
    return Results.Created($"/{tenantId}/responses", null);
});

app.MapDelete("{tenantId:guid}/responses", async (Guid tenantId, [FromQuery]string path, ResponseRepository responseRepo) =>
{
    if (string.IsNullOrWhiteSpace(path))
        return Results.BadRequest(new { error = "Path cannot be null or whitespace." });
    await responseRepo.DeleteResponse(tenantId, path);
    return Results.Ok();
});

app.Map("{tenantId:guid}/{**catchAll}", async (Guid tenantId, string catchAll, HttpRequest request, IRequestRepository repository, ResponseRepository responseRepo, CancellationToken ct) =>
{
    // Skip favicon requests
    if (request.Path.Value?.EndsWith("favicon.ico") == true)
        return Results.Ok();

    await repository.SaveRequest(request, tenantId, ct);

    var response = await responseRepo.FindResponse(tenantId, request.Path.Value ?? "/");
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
})
.WithName("CatchAll")
.WithOpenApi();

app.Run();
