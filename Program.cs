using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webhook.Navferty;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("WEBHOOK_");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<ResponseRepository>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Add Razor Pages services
builder.Services.AddRazorPages();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
appDbContext.Database.EnsureCreated();

// Configure the HTTP request pipeline.
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
    await responseRepo.ConfigureResponse(tenantId, dto.Path ?? "/", dto.Body);
    return Results.Created($"/{tenantId}/responses", null);
});

app.Map("{tenantId:guid}/{**catchAll}", async (Guid tenantId, HttpRequest request, IRequestRepository repository, ResponseRepository responseRepo, CancellationToken ct) =>
{
    // Skip favicon requests
    if (request.Path.Value?.EndsWith("favicon.ico") == true)
        return Results.Ok();

    await repository.SaveRequest(request, tenantId, ct);

    var response = await responseRepo.FindResponse(tenantId, request.Path.Value ?? "/");

    return response is not null
        ? Results.Json(response.Body)
        : Results.Json(new { message = "No response configured" });
})
.WithName("CatchAll")
.WithOpenApi();

app.Run();

public record CreateResponseDto(string Path, string Body);
