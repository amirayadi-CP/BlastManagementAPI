# Architecture & Design Patterns

## Overview

This document provides an in-depth explanation of the architectural patterns and design decisions in the Blast Management API.

## Event Sourcing Architecture

### What is Event Sourcing?

Event Sourcing is a pattern where **changes to application state are captured as a series of immutable events**. Instead of storing just the current state of an entity, we store every state-changing event that has occurred.

### How It Works

```
Commands → Aggregate → Events → Event Store
                        ↓
                    Projection
                        ↓
                    Read Model
                        ↓
                      Queries
```

1. **Command Arrives**: User issues a command (e.g., CreateBlastCommand)
2. **Aggregate Processes**: Aggregate validates rules and raises events
3. **Events Persisted**: Events are appended to the event store
4. **Projection Rebuilds**: Read model updates asynchronously
5. **Query Executes**: Query reads from the projection

### Event Store Design

```csharp
// In-memory implementation
Dictionary<Guid, List<IDomainEvent>> _eventStreams

// Example stream for blast "550e8400..."
BlastCreated (v1) → BlastLoaded (v2) → HoleAdded (v3) → HoleCharged (v4) → BlastFired (v5)
```

**Key Properties:**
- **Immutable**: Events never change or delete, only append
- **Ordered**: Events maintain sequence with version numbers
- **Versioned**: Each event knows its position in the stream
- **Aggregate-scoped**: Each aggregate has its own event stream

### State Reconstruction (Replay)

```csharp
// Load all events for a blast
var events = await eventStore.GetEventsAsync(blastId);

// Replay to rebuild state
var blast = new Blast();
foreach (var @event in events)
{
    blast.ApplyEvent(@event);  // State mutation happens here
}
// Now blast represents the current state
```

**Why Replay?**
- No need to store state separately
- Single source of truth (events)
- Temporal queries possible
- Complete audit trail

## CQRS Pattern

### What is CQRS?

CQRS stands for **Command Query Responsibility Segregation**. It separates read and write operations into distinct objects.

### Command Side (Write)

```
Client
  ↓
POST /blasts → CreateBlastCommand
  ↓
CreateBlastCommandHandler
  1. Validate (is name present?)
  2. Create aggregate
  3. Aggregate raises BlastCreated event
  4. Append event to event store
  5. Return result
  ↓
Event stored permanently
```

**Characteristics:**
- Changes state
- Executes business logic
- Validates invariants
- Raises events
- Returns command result

### Query Side (Read)

```
Client
  ↓
GET /blasts/{id} → GetBlastQuery
  ↓
GetBlastQueryHandler
  1. Query read model (projection)
  2. Return data immediately
  3. No state mutation
  ↓
Data returned (from cache)
```

**Characteristics:**
- Doesn't change state
- Reads from optimized store (projection)
- Fast and scalable
- No business logic

### CQRS Benefits

| Aspect | Monolithic | CQRS |
|--------|-----------|------|
| **Scalability** | Same for reads/writes | Independent scaling |
| **Optimization** | Compromise | Optimized per use case |
| **Consistency** | Strong | Eventual |
| **Complexity** | Lower | Higher |
| **Concurrency** | Locks | Optimistic |

## Aggregate Pattern

### What is an Aggregate?

An aggregate is a cluster of related entities that are treated as a single unit. It's the boundary of consistency.

### Blast Aggregate

```
Blast (Root)
├── Name
├── Status
├── DateBlasted
└── Holes[] (Child Entities)
    ├── Hole 1
    │   ├── Name
    │   ├── Position
    │   └── Status
    └── Hole 2
        ├── Name
        ├── Position
        └── Status
```

**Invariants:**
- Child holes belong to exactly one blast
- Cannot modify holes without going through blast
- Blast enforces business rules for the whole tree

### Command Methods

Aggregates expose command methods (not setters):

```csharp
// Good ✓
blast.AddHole(holeId, "H-01", position, direction, inclination);

// Bad ✗
blast.Holes.Add(new Hole { ... });  // Bypasses validation
```

**Why?**
- Validates preconditions
- Raises appropriate events
- Maintains invariants
- Encapsulates logic

## Optimistic Concurrency Control

### Problem
Two clients modify the same aggregate simultaneously:

```
Client A                    Client B
  ↓                           ↓
Load blast (v=5)      Load blast (v=5)
  ↓                           ↓
Charge hole              Mark hole ready
  ↓                           ↓
Append event (expect v=5) → Success (now v=6)
  ↓
Append event (expect v=5) → CONFLICT! (actual v=6)
```

### Solution: Version-Based Conflict Detection

```csharp
public async Task AppendEventsAsync(Guid aggregateId, 
    IEnumerable<IDomainEvent> events, 
    long expectedVersion)  // ← Version check
{
    var currentVersion = GetCurrentVersion(aggregateId);
    
    if (currentVersion != expectedVersion)
    {
        throw new InvalidOperationException(
            $"Concurrency conflict: expected {expectedVersion}, got {currentVersion}");
    }
    
    // Append is safe now
    AppendEvents(events);
}
```

**When does it happen?**
- Loading aggregate: `currentVersion = lastEvent.Version`
- After changes: `currentVersion += UncommittedEvents.Count`
- On append: Verify `currentVersion == expectedVersion`

## Read Model Pattern

### Problem
Every query replays ALL events → O(n) performance

```
10 queries × 1000 events × replay = 10,000 event replays!
```

### Solution: Pre-Built Projection

```
Events → Subscribe → ReadModel Cache → Fast Queries
                          ↓
                     O(1) lookup
```

### Implementation

```csharp
// Register subscription
eventStore.Subscribe(async @event => readModel.Handle(@event));

// Update projection asynchronously
public void Handle(IDomainEvent @event)
{
    // Apply event to read model
    // Example: HoleAdded → update _blasts[blastId].Holes
}

// Query reads from cache
public BlastDto? GetBlast(Guid id) => _blasts.TryGetValue(id, out var blast) ? blast : null;
```

### Trade-offs

| Aspect | Event Replay | Read Model |
|--------|-------------|-----------|
| **Consistency** | Strong (always up-to-date) | Eventual (slightly behind) |
| **Speed** | Slow (O(n)) | Fast (O(1)) |
| **Memory** | None (events only) | Extra (cache) |
| **Complexity** | Simple | More complex |

**When to use each:**
- **Replay**: Low-traffic queries, temporal queries, strong consistency needed
- **Read Model**: High-traffic queries, eventual consistency acceptable

## Domain-Driven Design

### Ubiquitous Language

Domain terms appear in code:

```csharp
// Domain language
blast.FireBlast(now);        // "Fire" is domain terminology
blast.MarkHoleReady(holeId); // "Mark ready" matches domain

// Not: blast.UpdateStatus("Blasted")
```

### Value Objects

Represent concepts without identity:

```csharp
public class Position
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    
    // Compared by value, not reference
    public override bool Equals(object? obj) { ... }
    public override int GetHashCode() { ... }
}

// Usage
var pos1 = new Position(100, 200, 300);
var pos2 = new Position(100, 200, 300);
Assert.Equal(pos1, pos2);  // ✓ True (same values)
```

### Aggregates vs. Repositories

We don't use repositories. Instead:
- Aggregates are event-sourced
- Event store is the repository
- `GetEventsAsync(id)` replays the aggregate

## Error Handling Strategy

### Business Rule Violations
Return HTTP 400 with error message:

```csharp
if (blast.Status == BlastStatus.Blasted)
    return Results.BadRequest(new { message = "Cannot fire an already-fired blast" });
```

### Not Found
Return HTTP 404:

```csharp
var events = await eventStore.GetEventsAsync(blastId);
if (!events.Any())
    return Results.NotFound(new { message = $"Blast {blastId} not found" });
```

### Concurrency Conflict
Return HTTP 409:

```csharp
try
{
    await eventStore.AppendEventsAsync(id, events, expectedVersion);
}
catch (InvalidOperationException)
{
    return Results.Conflict(new { message = "Concurrency conflict" });
}
```

## Initialization & Dependency Injection

### Program.cs Setup

```csharp
// Register services
builder.Services.AddScoped<IEventStore, InMemoryEventStore>();
builder.Services.AddSingleton<BlastReadModel>();

// Setup event subscription for read model
var eventStore = app.Services.GetRequiredService<IEventStore>();
var readModel = app.Services.GetRequiredService<BlastReadModel>();

eventStore.Subscribe(async e => readModel.Handle(e));
```

**Why this structure?**
- `IEventStore`: Scoped (one per request) but backing data is shared via DI
- `BlastReadModel`: Singleton (shared across requests)
- Subscription happens once at startup

## Testing Strategy

### Unit Tests (Aggregates)
```csharp
[Test]
public void FireBlast_WithUnreadyHoles_ThrowsException()
{
    var blast = Blast.CreateBlast(id, "B-042");
    var hole = new Hole(holeId, id, "H-01", pos, 45, -15);
    blast.AddHole(...);
    
    Assert.Throws<InvalidOperationException>(() => blast.FireBlast(now));
}
```

### Integration Tests (Handler + Event Store)
```csharp
[Test]
public async Task CreateBlastCommand_AppendsBlastCreatedEvent()
{
    var handler = new CreateBlastCommandHandler(eventStore);
    var result = await handler.HandleAsync(new CreateBlastCommand { Name = "B-042" });
    
    var events = await eventStore.GetEventsAsync(result.AggregateId);
    Assert.Single(events);
    Assert.IsType<BlastCreated>(events.First());
}
```

### Acceptance Tests (API)
```csharp
[Test]
public async Task CreateBlast_Returns201Created()
{
    var response = await client.PostAsJsonAsync("/blasts", 
        new { name = "B-042" });
    
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

## Deployment & Scalability

### Current: In-Process
- Single instance
- In-memory event store
- Good for: Learning, testing, small deployments

### Future: Distributed
```
Client
  ↓
API Instance 1 ────┐
API Instance 2 ────┼──→ Event Store (Shared DB)
API Instance 3 ────┘      ↓
                    Read Model Cache (Redis)
```

**Changes needed:**
- Replace `InMemoryEventStore` with persistent storage
- Add distributed locking (optimistic concurrency won't work across instances)
- Cache read models in Redis/Memcached

## Summary

| Pattern | Purpose | Benefit |
|---------|---------|---------|
| **Event Sourcing** | Store state changes as events | Audit trail, temporal queries |
| **CQRS** | Separate read and write | Independent scaling, optimization |
| **Aggregates** | Consistency boundary | Encapsulation, invariant enforcement |
| **Optimistic Concurrency** | Conflict detection | No blocking locks |
| **Read Models** | Pre-computed views | Fast queries |
| **DDD** | Language alignment | Clear code, reduced bugs |

---

See [README.md](README.md) for API usage and [IMPLEMENTATION.md](IMPLEMENTATION.md) for technical details.
