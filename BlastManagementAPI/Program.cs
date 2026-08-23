using BlastManagementAPI.API.Endpoints;
using BlastManagementAPI.Infrastructure.EventStore;
using BlastManagementAPI.Infrastructure.Projections;

var builder = WebApplication.CreateBuilder(args);

// Add services
// Event store is a singleton because it's the persistence layer and must be shared across all requests
builder.Services.AddSingleton<IEventStore, InMemoryEventStore>();
builder.Services.AddSingleton<BlastReadModel>();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Setup event subscription for read model projection
// This demonstrates the bonus feature: a pre-built read model that gets updated as events occur.
// The projection subscribes to all events, so GetBlast reads from this cache instead of replaying.
var eventStore = app.Services.GetRequiredService<IEventStore>();
var readModel = app.Services.GetRequiredService<BlastReadModel>();

eventStore.Subscribe(async e =>
{
    readModel.Handle(e);
    return;
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map endpoints
app.MapBlastEndpoints();

// Health check
app.MapGet("/health", () => Results.Ok("Blast Management API is running."))
    .WithName("HealthCheck")
    .WithDescription("Health check endpoint");

app.Run();
