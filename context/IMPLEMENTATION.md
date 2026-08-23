# Implementation Guide

## Quick Reference

### File Structure

```
BlastManagementAPI/
├── Domain/                 # Core business logic
│   ├── Aggregates/
│   │   ├── Blast.cs       # Main aggregate root
│   │   └── Hole.cs        # Child entity
│   ├── Events/            # Immutable event definitions
│   │   ├── IDomainEvent.cs
│   │   ├── BlastEvents.cs
│   │   └── HoleEvents.cs
│   ├── Enums.cs
│   └── Position.cs
├── Application/           # CQRS implementation
│   ├── Commands/
│   │   ├── Commands.cs
│   │   └── CommandHandlers.cs
│   └── Queries/
│       ├── Queries.cs
│       └── QueryHandlers.cs
├── Infrastructure/        # Persistence & caching
│   ├── EventStore/
│   │   └── IEventStore.cs (InMemoryEventStore impl)
│   └── Projections/
│       └── BlastReadModel.cs
├── API/                   # HTTP layer
│   ├── Endpoints/
│   │   └── BlastEndpoints.cs
│   └── DTOs/
│       └── Requests.cs
└── Program.cs
```

## Key Files Explained

### 1. Domain Layer

#### `Domain/Aggregates/Blast.cs`
- **Aggregate Root** for the bounded context
- **Responsibility**: Enforce invariants, raise events
- **Key Methods**:
  - `CreateBlast()`: Factory method
  - `AddHole()`: Add hole to blast
  - `ChargeHole()`: Charge a hole
  - `MarkHoleReady()`: Mark hole as ready (bonus)
  - `FireBlast()`: Fire the blast
  - `ApplyEvent()`: Replay events to rebuild state

**Design Notes:**
```csharp
// No property setters — encapsulation is strict
public string Name { get; private set; }  // Only this class can set

// Command methods raise events
public void FireBlast(DateTimeOffset now)
{
    // Validate
    if (Status == BlastStatus.Blasted)
        throw new InvalidOperationException("Already fired");
    
    // Enforce invariant: all holes must be Ready
    var notReady = _holes.Where(h => h.Status != HoleStatus.Ready).ToList();
    if (notReady.Any())
        throw new InvalidOperationException($"Not ready: {notReady}");
    
    // Change state by raising event
    Status = BlastStatus.Blasted;
    DateBlasted = now;
    
    var @event = new BlastFired(Id, now) { Version = Version + 1, ... };
    _uncommittedEvents.Add(@event);  // Queue for persistence
    Version++;
}
```

#### `Domain/Aggregates/Hole.cs`
- **Child Entity** (not aggregate root)
- Belongs to exactly one Blast
- Validates local rules (e.g., can't charge twice)

#### `Domain/Events/` (BlastEvents, HoleEvents)
- **Immutable event definitions**
- Named in past tense (BlastFired, HoleCharged)
- Capture all relevant data for replay

**Event Design Pattern:**
```csharp
public class HoleCharged : IDomainEvent
{
    public Guid AggregateId { get; set; }      // Which blast?
    public long Version { get; set; }          // Sequence #
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(HoleCharged);
    
    public required Guid HoleId { get; init; } // Which hole?
    
    // Required init-only: immutable, no setters
}
```

### 2. Application Layer

#### `Application/Commands/Commands.cs`
- **Command definitions** (data transfer objects for write operations)
- No logic, just data containers
- Example:
```csharp
public class CreateBlastCommand : ICommand
{
    public required string Name { get; init; }
}

public class FireBlastCommand : ICommand
{
    public required Guid BlastId { get; init; }
}
```

#### `Application/Commands/CommandHandlers.cs`
- **Command execution logic**
- Steps:
  1. Load aggregate from event store
  2. Replay events to rebuild state
  3. Execute command on aggregate
  4. Persist new events
  5. Return result

**Handler Pattern:**
```csharp
public class FireBlastCommandHandler : ICommandHandler<FireBlastCommand>
{
    private readonly IEventStore _eventStore;
    
    public async Task<CommandResult> HandleAsync(FireBlastCommand cmd)
    {
        // 1. Load
        var events = await _eventStore.GetEventsAsync(cmd.BlastId);
        if (!events.Any())
            return new CommandResult { Success = false, Message = "Not found" };
        
        // 2. Rebuild
        var blast = new Blast();
        var lastVersion = 0L;
        foreach (var e in events)
        {
            blast.ApplyEvent(e);
            lastVersion = e.Version;
        }
        
        // 3. Execute
        try
        {
            blast.FireBlast(DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
        
        // 4. Persist
        await _eventStore.AppendEventsAsync(
            cmd.BlastId, 
            blast.UncommittedEvents, 
            lastVersion  // Optimistic concurrency check
        );
        
        // 5. Return
        return new CommandResult { Success = true, AggregateId = cmd.BlastId };
    }
}
```

#### `Application/Queries/Queries.cs`
- **Query definitions** (read requests)
- No side effects
```csharp
public class GetBlastQuery : IQuery
{
    public required Guid BlastId { get; init; }
}
```

#### `Application/Queries/QueryHandlers.cs`
- **Query execution** (read operations)
- `GetBlastQueryHandler`: Read from projection (fast)
- `GetBlastHistoryQueryHandler`: Read raw events

```csharp
public class GetBlastQueryHandler : IQueryHandler<GetBlastQuery, BlastDto?>
{
    private readonly BlastReadModel _readModel;
    
    public async Task<BlastDto?> HandleAsync(GetBlastQuery query)
    {
        // Read from pre-built cache — O(1) fast!
        return _readModel.GetBlast(query.BlastId);
    }
}
```

### 3. Infrastructure Layer

#### `Infrastructure/EventStore/IEventStore.cs`
- **Event persistence interface**
- **In-memory implementation**: Dictionary<aggregateId, List<events>>

**Key methods:**
```csharp
// Append events with optimistic concurrency
Task AppendEventsAsync(Guid aggregateId, 
    IEnumerable<IDomainEvent> events, 
    long expectedVersion)

// Load full stream
Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId)

// Subscribe for read model updates
void Subscribe(Func<IDomainEvent, Task> handler)
```

**How Concurrency Works:**
```csharp
public async Task AppendEventsAsync(Guid id, IEnumerable<IDomainEvent> events, long expected)
{
    lock (_lock)  // Thread-safe
    {
        var stream = _eventStreams[id];
        long current = stream.Any() ? stream.Last().Version : 0;
        
        if (current != expected)  // Concurrency check!
            throw new InvalidOperationException(
                $"Expected version {expected}, but got {current}");
        
        stream.AddRange(events);  // Append
    }
    
    // Notify subscribers outside lock
    foreach (var subscriber in _subscribers)
        await subscriber(@event);
}
```

#### `Infrastructure/Projections/BlastReadModel.cs`
- **Read model cache** (bonus feature)
- Subscribes to events
- Updates asynchronously
- Serves queries via `GetBlast(id)` → O(1) lookup

**Subscription Flow:**
```
Event Store emits BlastCreated
    ↓
ReadModel.Handle(BlastCreated)
    ↓
_blasts[id] = new BlastDto { Name = "B-042", Status = "Planned", ... }
    ↓
GetBlastQuery reads from _blasts[id] immediately (fast!)
```

### 4. API Layer

#### `API/DTOs/Requests.cs`
- **HTTP request/response objects**
- Transfer data between client and server
- Separate from domain models

#### `API/Endpoints/BlastEndpoints.cs`
- **HTTP endpoint handlers**
- Map routes → commands/queries
- Handle HTTP specifics (status codes, serialization)

**Endpoint Pattern:**
```csharp
public static void MapBlastEndpoints(this WebApplication app)
{
    app.MapPost("/blasts", CreateBlast)
       .WithName("CreateBlast");
}

private static async Task<IResult> CreateBlast(
    CreateBlastRequest request,
    IEventStore eventStore)
{
    var handler = new CreateBlastCommandHandler(eventStore);
    var cmd = new CreateBlastCommand { Name = request.Name };
    var result = await handler.HandleAsync(cmd);
    
    if (!result.Success)
        return Results.BadRequest(...);
    
    return Results.Created($"/blasts/{result.AggregateId}", ...);
}
```

### 5. Program.cs
- **Application startup**
- DI registration
- Event subscription setup

```csharp
// Register services
builder.Services.AddScoped<IEventStore, InMemoryEventStore>();
builder.Services.AddSingleton<BlastReadModel>();

// Setup read model subscription
var eventStore = app.Services.GetRequiredService<IEventStore>();
var readModel = app.Services.GetRequiredService<BlastReadModel>();
eventStore.Subscribe(e => readModel.Handle(e));
```

## Common Patterns

### Loading an Aggregate

```csharp
// 1. Get all events
var events = await eventStore.GetEventsAsync(aggregateId);

// 2. Create empty aggregate
var aggregate = new Aggregate();

// 3. Replay each event in order
var lastVersion = 0L;
foreach (var @event in events)
{
    aggregate.ApplyEvent(@event);
    lastVersion = @event.Version;
}

// 4. Aggregate is now in its current state
// lastVersion is used for optimistic concurrency on next write
```

### Creating & Persisting an Aggregate

```csharp
// 1. Create aggregate (raises events but doesn't persist)
var blast = Blast.CreateBlast(Guid.NewGuid(), "B-042");
// blast.UncommittedEvents contains [BlastCreated]

// 2. Append to event store
await eventStore.AppendEventsAsync(
    blast.Id,
    blast.UncommittedEvents,
    0  // Expected version (new aggregate, so 0)
);

// 3. Clear uncommitted events
blast.ClearUncommittedEvents();
```

### Handling Command with Side Effects

```csharp
var handler = new SomeCommandHandler(eventStore);
var result = await handler.HandleAsync(command);

if (!result.Success)
{
    // Log, return error to client
    return Results.BadRequest(result.Message);
}

// Success
return Results.Ok(new { message = result.Message });
```

## Important Design Decisions

### 1. No Setters on Aggregates
```csharp
// ✓ Good: Validation happens
blast.AddHole(holeId, "H-01", position, dir, incl);

// ✗ Bad: Bypasses validation
blast.Holes.Add(new Hole { ... });
```

### 2. Events Over State
```csharp
// ✗ Don't do this
public class Blast
{
    public void UpdateStatus(string newStatus) { ... }  // Wrong!
}

// ✓ Do this
public class Blast
{
    public void FireBlast() { ... }  // Raises BlastFired event
}
```

### 3. Immutable Events
```csharp
// ✓ Events can't change
public required Guid HoleId { get; init; }  // init-only

// ✗ Never do this
public Guid HoleId { get; set; }  // Mutable!
```

### 4. Optimistic Over Pessimistic
```csharp
// ✓ Optimistic: Fast, no locks
if (currentVersion != expectedVersion)
    throw new Exception("Conflict");

// ✗ Pessimistic: Slow, requires locks
lock (mutex) { ... }
```

### 5. Read Model is Optional
- Commands always rebuild via replay
- Queries prefer read model but could replay
- Flexibility: swap read model source without changing API

## Extension Points

### Adding a New Command

1. **Define command** in `Commands/Commands.cs`
   ```csharp
   public class SomeCommand : ICommand { public required Data X { get; init; } }
   ```

2. **Implement handler** in `Commands/CommandHandlers.cs`
   ```csharp
   public class SomeCommandHandler : ICommandHandler<SomeCommand>
   {
       public async Task<CommandResult> HandleAsync(SomeCommand cmd) { ... }
   }
   ```

3. **Add HTTP endpoint** in `API/Endpoints/BlastEndpoints.cs`
   ```csharp
   app.MapPost("/path", SomeEndpoint);
   private static async Task<IResult> SomeEndpoint(...) { ... }
   ```

### Adding a New Query

1. **Define query** in `Queries/Queries.cs`
   ```csharp
   public class SomeQuery : IQuery { public required Guid Id { get; init; } }
   ```

2. **Implement handler** in `Queries/QueryHandlers.cs`
   ```csharp
   public class SomeQueryHandler : IQueryHandler<SomeQuery, ResultDto?>
   {
       public async Task<ResultDto?> HandleAsync(SomeQuery q) { ... }
   }
   ```

3. **Add HTTP endpoint** in `API/Endpoints/BlastEndpoints.cs`
   ```csharp
   app.MapGet("/path/{id}", SomeEndpoint);
   ```

### Adding a New Event

1. **Define event** in `Domain/Events/[DomainName]Events.cs`
   ```csharp
   public class SomethingHappened : IDomainEvent { ... }
   ```

2. **Update aggregate** to emit event
   ```csharp
   public void SomeCommand()
   {
       // Logic
       var @event = new SomethingHappened(...);
       _uncommittedEvents.Add(@event);
   }
   ```

3. **Update `ApplyEvent`** to handle event
   ```csharp
   public void ApplyEvent(IDomainEvent @event)
   {
       switch (@event)
       {
           case SomethingHappened e:
               // Update state
               break;
       }
   }
   ```

4. **Update read model** to handle event
   ```csharp
   public void Handle(IDomainEvent @event)
   {
       switch (@event)
       {
           case SomethingHappened e:
               // Update cache
               break;
       }
   }
   ```

## Testing Guidelines

### Test Aggregates Directly
```csharp
[Test]
public void FireBlast_WithUnreadyHole_Throws()
{
    var blast = Blast.CreateBlast(id, "B-042");
    blast.AddHole(...);  // Hole is in Planned state
    
    Assert.Throws<InvalidOperationException>(
        () => blast.FireBlast(now));
}
```

### Test Handlers with Event Store
```csharp
[Test]
public async Task Handler_AppendsEvent()
{
    var handler = new SomeCommandHandler(eventStore);
    var result = await handler.HandleAsync(cmd);
    
    Assert.True(result.Success);
    var events = await eventStore.GetEventsAsync(result.AggregateId);
    Assert.Single(events);
}
```

### Test Endpoints with HttpClient
```csharp
[Test]
public async Task Endpoint_Returns200()
{
    var response = await client.PostAsJsonAsync("/blasts", request);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

## Debugging Tips

### Trace Event Replay
```csharp
var events = await eventStore.GetEventsAsync(blastId);
foreach (var @event in events)
{
    Console.WriteLine($"v{@event.Version}: {@event.EventType} @ {event.Timestamp}");
    blast.ApplyEvent(@event);
}
```

### Check Read Model Cache
```csharp
var blast = readModel.GetBlast(blastId);
Console.WriteLine($"Cached state: {blast?.Status}");
```

### Verify Concurrency Detection
```csharp
var lastVersion = 5;
var actualVersion = 6;
if (lastVersion != actualVersion)
    Console.WriteLine("Concurrency conflict detected!");
```

---

See [ARCHITECTURE.md](ARCHITECTURE.md) for deeper design patterns and [README.md](README.md) for API usage.
