---
name: Blast API Patterns
description: "Coding patterns and best practices for the Blast Management API. Apply to all C# files in the BlastManagementAPI project."
applyTo:
  - "BlastManagementAPI/**/*.cs"
---

# Blast Management API — Coding Patterns & Best Practices

## Code Organization Principles

### 1. Domain Layer Has No Dependencies
- ✅ **Allowed**: Logic, validation, event raising
- ❌ **Not Allowed**: HTTP, database, external services, logging
- ❌ **Not Allowed**: Using IEventStore or ICommandHandler

**Example - Good:**
```csharp
public void FireBlast(DateTimeOffset now)
{
    if (Status == BlastStatus.Blasted)
        throw new InvalidOperationException("Already fired");
    
    Status = BlastStatus.Blasted;
    DateBlasted = now;
    
    var @event = new BlastFired { AggregateId = Id, DateBlasted = now };
    _uncommittedEvents.Add(@event);
}
```

**Example - Bad:**
```csharp
public async Task FireBlast()
{
    await _eventStore.AppendEventsAsync(...);  // ❌ Domain shouldn't know about store
    _logger.LogInformation(...);               // ❌ Domain shouldn't log
}
```

### 2. Aggregates Are Immutable State Machines
- State changes only via command methods
- No public setters on properties
- All state read via `{ get; private set; }`
- Events raised inside command methods

```csharp
public class Blast
{
    // ✅ Read-only state
    public Guid Id { get; private set; }
    public BlastStatus Status { get; private set; }
    public IReadOnlyList<Hole> Holes => _holes.AsReadOnly();
    
    // ❌ Never expose mutable collection
    // public List<Hole> Holes { get; set; }  // BAD
    
    // Command method: only way to change state
    public void FireBlast(DateTimeOffset now) { ... }
}
```

### 3. Events Are the Single Source of Truth
- Never store state outside events
- Always rebuild via replay
- Events are immutable (use `required init`)
- Events named in past tense

```csharp
// ✅ Good event
public class HoleCharged : IDomainEvent
{
    public Guid AggregateId { get; set; }
    public required Guid HoleId { get; init; }
    public long Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(HoleCharged);
}

// ❌ Bad event (mutable, future tense)
public class ChargeHole : IDomainEvent  // Wrong: imperative name
{
    public Guid HoleId { get; set; }    // Wrong: mutable
}
```

### 4. CQRS Separation Is Strict
- Command handler: load → execute → append → return
- Query handler: read from cache or replay (no mutations)
- Different objects, different handlers
- No shared logic between write and read

```csharp
// ✅ Command handler: Modifies state
public class FireBlastCommandHandler : ICommandHandler<FireBlastCommand>
{
    public async Task<CommandResult> HandleAsync(FireBlastCommand cmd)
    {
        var blast = new Blast();
        // Load, execute, append
        return new CommandResult { Success = true };
    }
}

// ✅ Query handler: Only reads
public class GetBlastQueryHandler : IQueryHandler<GetBlastQuery, BlastDto?>
{
    public async Task<BlastDto?> HandleAsync(GetBlastQuery query)
    {
        return _readModel.GetBlast(query.BlastId);  // Read only
    }
}

// ❌ Shared handler (wrong)
public class BlastHandler  // BAD: "blast handler" not specific
{
    public void Execute(ICommand cmd) { ... }
    public BlastDto Get(Guid id) { ... }
}
```

### 5. Concurrency Via Versioning
- Always check version on append
- Version = sequence number in event stream
- Load aggregate → know lastVersion → append with lastVersion check

```csharp
// ✅ Handler pattern
var events = await _eventStore.GetEventsAsync(aggregateId);
var blast = new Blast();
var lastVersion = 0L;

foreach (var e in events) {
    blast.ApplyEvent(e);
    lastVersion = e.Version;
}

blast.DoSomething();

// Optimistic concurrency check here
await _eventStore.AppendEventsAsync(
    aggregateId, 
    blast.UncommittedEvents, 
    lastVersion  // Must match!
);

// ❌ Forgetting version check
await _eventStore.AppendEventsAsync(aggregateId, events);  // No version!
```

## Event Flow Checklist

When adding a new domain event, follow this checklist:

- [ ] Event class created in `Domain/Events/[Domain]Events.cs`
- [ ] Properties use `required init` (immutable)
- [ ] Implements `IDomainEvent` with `AggregateId`, `Version`, `Timestamp`, `EventType`
- [ ] Named in past tense (e.g., `HoleCharged`, not `ChargeHole`)
- [ ] Aggregate command method raises the event
- [ ] Aggregate.ApplyEvent() handles the event
- [ ] BlastReadModel.Handle() updates cache (if needed)
- [ ] GetBlastHistoryQueryHandler.SerializeEvent() includes the event
- [ ] HTTP endpoint maps to command if user-initiated

## Naming Conventions

### Commands
- Name: `[Action]Command` (e.g., `CreateBlastCommand`, `FireBlastCommand`)
- Handler: `[Action]CommandHandler`
- Verb form (imperative)

### Events
- Name: `[Action]ed` (past tense, e.g., `BlastCreated`, `HoleCharged`)
- Always past tense
- No "Command" or "Event" suffix (inherits from IDomainEvent)

### Queries
- Name: `Get[Entity][Optional]Query` (e.g., `GetBlastQuery`, `GetBlastHistoryQuery`)
- Handler: `[Name]QueryHandler`
- Returns DTOs with `Dto` suffix or `record` type

### HTTP Endpoints
- POST: `/blasts` → CreateBlastCommand
- PUT: `/blasts/{id}/holes/{holeId}/charge` → ChargeHoleCommand
- GET: `/blasts/{id}` → GetBlastQuery
- Status 201 for created, 400 for validation error, 404 for not found

## Property Declaration Standards

### Domain Entities
```csharp
// ✅ Standard aggregate property
public string Name { get; private set; }

// ✅ Collection (read-only)
public IReadOnlyList<Hole> Holes => _holes.AsReadOnly();
private readonly List<Hole> _holes = new();

// ✅ Version tracking
public long Version { get; private set; }
```

### DTOs (Records)
```csharp
// ✅ Immutable DTO
public record BlastDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset? DateBlasted { get; init; }
}
```

### Events
```csharp
// ✅ Immutable event
public class HoleAdded : IDomainEvent
{
    public Guid AggregateId { get; set; }
    public required Guid HoleId { get; init; }
    public required string Name { get; init; }
    public long Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(HoleAdded);
}
```

## HTTP Status Codes

Map business outcomes to HTTP status codes:

| Code | Scenario | Example |
|------|----------|---------|
| 200 OK | Query successful | `GET /blasts/{id}` returns blast |
| 201 Created | Command successful, new resource | `POST /blasts` creates blast |
| 400 Bad Request | Business rule violated | Cannot charge already-charged hole |
| 404 Not Found | Aggregate not found | `GET /blasts/{id}` with unknown ID |
| 409 Conflict | Concurrency version mismatch | Two clients modify same aggregate |

```csharp
// ✅ Endpoint pattern
if (!result.Success)
{
    return result.Message?.Contains("not found") ?? false
        ? Results.NotFound(new { message = result.Message })
        : Results.BadRequest(new { message = result.Message });
}

return result.IsCreation
    ? Results.Created($"/blasts/{result.AggregateId}", response)
    : Results.Ok(response);
```

## Error Handling Strategy

### Aggregate Validation Errors
Raise `InvalidOperationException` in aggregate methods:
```csharp
if (Status == BlastStatus.Blasted)
    throw new InvalidOperationException("Cannot add holes to blasted blast.");
```

### Input Validation Errors
Check in command handler before loading aggregate:
```csharp
if (string.IsNullOrWhiteSpace(command.Name))
    return new CommandResult { Success = false, Message = "Name required" };
```

### Not Found Errors
Check after loading events:
```csharp
var events = await _eventStore.GetEventsAsync(aggregateId);
if (!events.Any())
    return new CommandResult { Success = false, Message = "Blast not found" };
```

### Concurrency Errors
Let exception from event store propagate, catch, and return 409:
```csharp
try {
    await _eventStore.AppendEventsAsync(...);
} catch (InvalidOperationException) {
    return new CommandResult { Success = false, Message = "Concurrency conflict" };
}
```

## DI & Service Registration

### Program.cs Pattern
```csharp
// Single event store (shared)
builder.Services.AddScoped<IEventStore, InMemoryEventStore>();

// Singleton read model
builder.Services.AddSingleton<BlastReadModel>();

// Subscribe read model to events
var eventStore = app.Services.GetRequiredService<IEventStore>();
var readModel = app.Services.GetRequiredService<BlastReadModel>();
eventStore.Subscribe(e => readModel.Handle(e));
```

### Handler Injection
```csharp
private static async Task<IResult> EndpointName(
    IEventStore eventStore,
    BlastReadModel readModel)
{
    var handler = new SomeCommandHandler(eventStore);
    var result = await handler.HandleAsync(command);
    return ...;
}
```

## Testing Patterns

### Aggregate Unit Test
```csharp
[Test]
public void FireBlast_WithUnreadyHole_Throws()
{
    var blast = Blast.CreateBlast(blastId, "B-042");
    blast.AddHole(holeId, "H-01", pos, 45, -15);
    // Hole is Planned, not Ready
    
    Assert.Throws<InvalidOperationException>(
        () => blast.FireBlast(DateTimeOffset.UtcNow));
}
```

### Handler Integration Test
```csharp
[Test]
public async Task FireBlastCommand_AppendsBlastFiredEvent()
{
    var eventStore = new InMemoryEventStore();
    var handler = new FireBlastCommandHandler(eventStore);
    
    // Create blast first
    await new CreateBlastCommandHandler(eventStore)
        .HandleAsync(new CreateBlastCommand { Name = "B-042" });
    
    // Fire it
    var result = await handler.HandleAsync(
        new FireBlastCommand { BlastId = blastId });
    
    Assert.True(result.Success);
    var events = await eventStore.GetEventsAsync(blastId);
    Assert.Contains(e => e is BlastFired, events);
}
```

## Common Mistakes to Avoid

| Mistake | Impact | Fix |
|---------|--------|-----|
| Adding setters to aggregate properties | State inconsistency | Use `{ get; private set; }` only |
| Logging in domain layer | Tight coupling | Move logging to handlers/endpoints |
| Querying uncommitted events | Stale data | Always clear after append |
| Forgetting version check | Concurrency bugs | Always pass lastVersion to append |
| Naming events imperatively | Confusion | Use past tense (Created, Charged) |
| Modifying events after creation | Lost audit trail | Events are immutable |
| Query handlers mutating state | CQRS violation | Queries are read-only |
| Business logic in endpoints | Hard to test | Move to aggregates/handlers |
| Multiple event types in one event | Complex replay | One event = one fact |

---

**Remember**: Events are your audit trail. Make them clear, immutable, and expressive.
