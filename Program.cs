using Microsoft.EntityFrameworkCore;
using Webhook.Navferty;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("WEBHOOK_");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IRequestRepository, RequestRepository>();

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
    context.Response.Redirect("/Index");
});

app.MapGet("/requests/{id:guid}", async (Guid id, IRequestRepository repository) =>
{
    var request = await repository.GetRequest(id);
    return request is not null ? Results.Ok(request) : Results.NotFound();
})
.WithName("GetRequestById")
.WithOpenApi();

app.MapGet("/requests", async (DateTimeOffset from, DateTimeOffset to, IRequestRepository repository) =>
{
    var requests = await repository.GetRequests(from, to);
    return Results.Ok(requests);
})
.WithName("GetRequests")
.WithOpenApi();

app.Map("/{**catchAll}", async (HttpRequest request, IRequestRepository repository, CancellationToken ct) =>
{
    // Skip favicon requests
    if (request.Path.Value?.EndsWith("favicon.ico") == true)
        return Results.Ok();

    await repository.SaveRequest(request, ct);
    return Results.Ok();
})
.WithName("CatchAll")
.WithOpenApi();

app.Run();
