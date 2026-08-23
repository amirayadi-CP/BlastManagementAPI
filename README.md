# Blast Management API — CQRS & Event Sourcing

A comprehensive implementation of a blast management system for mining operations, demonstrating modern .NET design patterns including **CQRS (Command Query Responsibility Segregation)** and **Event Sourcing**.

## Quick Start

### Prerequisites
- .NET 8.0 SDK or later

### Build & Run

```bash
cd BlastManagementAPI
dotnet restore
dotnet build
dotnet run
```

The API will start at `https://localhost:5001` (development) with Swagger UI available at `/swagger`.

## Architecture Overview

### Layered Design

```
┌─────────────────────────────────────────────────────┐
│           API Layer (Minimal API)                    │
│      • Endpoints & HTTP Routing                     │
│      • Request/Response DTOs                        │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│       Application Layer (CQRS)                       │
│  • Commands & Command Handlers (Write Side)         │
│  • Queries & Query Handlers (Read Side)             │
│  • Service Orchestration                            │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│       Domain Layer (Event Sourcing)                  │
│  • Aggregate Roots (Blast, Hole)                    │
│  • Domain Events (BlastCreated, HoleCharged, etc.)  │
│  • Value Objects (Position, Status Enums)           │
│  • Business Rules & Invariants                      │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│      Infrastructure Layer (Event Store)              │
│  • Event Persistence (In-Memory)                    │
│  • Read Model / Projections                         │
│  • Event Subscriptions                              │
└─────────────────────────────────────────────────────┘
```

## Core Concepts

### 1. Event Sourcing
**State is derived exclusively by replaying events.** No mutable records or UPDATE statements.

- **Events**: Immutable records of what happened (e.g., `BlastCreated`, `HoleCharged`, `BlastFired`)
- **Streams**: Ordered sequences of events per aggregate
- **Replay**: State rebuilt by replaying events in order
- **Audit Trail**: Complete history of all changes available

**Benefits:**
- Temporal queries: "What was the state at time T?"
- Complete audit log
- Event-driven architecture readiness
- Time travel / snapshots

### 2. CQRS (Command Query Responsibility Segregation)
**Separate objects for write (commands) and read (queries).**

**Write Side (Commands):**
- Validate business rules
- Apply commands to aggregates
- Raise domain events
- Persist events to the event store

**Read Side (Queries):**
- Query pre-built read models (projections)
- No business logic, no state mutations
- Optimized for fast reads

**Benefits:**
- Scales reads and writes independently
- Clear separation of concerns
- Supports eventual consistency
- Enables different data models for read/write

### 3. Aggregates with Bounded Contexts
**Blast** is the aggregate root; **Hole** is a child entity.

- **Blast aggregate:**
  - Contains holes
  - Enforces invariants (e.g., cannot fire with unready holes)
  - Raises domain events
  - Stateless: state comes from event replay only

- **No setters:** Properties are private. State changes only through command methods.

### 4. Optimistic Concurrency Control
The event store tracks **version numbers** to detect concurrent modifications.

When appending events, the expected version must match the current stream version. If not, a `InvalidOperationException` is raised.

## API Endpoints

### Create Blast
```http
POST /blasts
Content-Type: application/json

{
  "name": "B-042"
}

201 Created
{
  "success": true,
  "message": "Blast 'B-042' created successfully.",
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

### Add Hole
```http
POST /blasts/{blastId}/holes
Content-Type: application/json

{
  "name": "H-01",
  "x": 100.5,
  "y": 200.3,
  "z": -50.2,
  "direction": 45,
  "inclination": -15
}

200 OK
{
  "success": true,
  "message": "Hole 'H-01' added to blast '550e8400-e29b-41d4-a716-446655440000'."
}
```

### Charge Hole
```http
PUT /blasts/{blastId}/holes/{holeId}/charge

200 OK
{
  "success": true,
  "message": "Hole '550e8400-e29b-41d4-a716-446655440001' charged successfully."
}
```

### Mark Hole Ready
```http
PUT /blasts/{blastId}/holes/{holeId}/ready

200 OK
{
  "success": true,
  "message": "Hole '550e8400-e29b-41d4-a716-446655440001' marked as ready."
}
```

### Fire Blast
```http
POST /blasts/{blastId}/fire

200 OK
{
  "success": true,
  "message": "Blast '550e8400-e29b-41d4-a716-446655440000' fired successfully at 2026-08-23T12:34:56Z"
}
```

### Get Blast (Read Model)
```http
GET /blasts/{blastId}

200 OK
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "B-042",
    "status": "Blasted",
    "dateBlasted": "2026-08-23T12:34:56Z",
    "holes": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440001",
        "name": "H-01",
        "x": 100.5,
        "y": 200.3,
        "z": -50.2,
        "direction": 45,
        "inclination": -15,
        "status": "Ready"
      }
    ]
  }
}
```

### Get Blast History
```http
GET /blasts/{blastId}/history

200 OK
{
  "success": true,
  "data": [
    {
      "version": 1,
      "eventType": "BlastCreated",
      "timestamp": "2026-08-23T12:30:00Z",
      "data": { "name": "B-042" }
    },
    {
      "version": 2,
      "eventType": "BlastLoaded",
      "timestamp": "2026-08-23T12:31:00Z",
      "data": {}
    },
    {
      "version": 3,
      "eventType": "HoleAdded",
      "timestamp": "2026-08-23T12:32:00Z",
      "data": {
        "holeId": "550e8400-e29b-41d4-a716-446655440001",
        "name": "H-01",
        "position": { "x": 100.5, "y": 200.3, "z": -50.2 },
        "direction": 45,
        "inclination": -15
      }
    },
    {
      "version": 4,
      "eventType": "HoleCharged",
      "timestamp": "2026-08-23T12:33:00Z",
      "data": { "holeId": "550e8400-e29b-41d4-a716-446655440001" }
    },
    {
      "version": 5,
      "eventType": "HoleMarkedReady",
      "timestamp": "2026-08-23T12:33:30Z",
      "data": { "holeId": "550e8400-e29b-41d4-a716-446655440001" }
    },
    {
      "version": 6,
      "eventType": "BlastFired",
      "timestamp": "2026-08-23T12:34:56Z",
      "data": { "dateBlasted": "2026-08-23T12:34:56Z" }
    }
  ]
}
```

## Domain Model

### Blast

| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | Unique identifier |
| `Name` | string | Human-readable label (e.g., "B-042") |
| `DateBlasted` | DateTimeOffset? | Null until the blast fires |
| `Status` | enum | Planned → Loaded → Blasted |
| `Holes` | List<Hole> | Child holes in the blast |

### Hole

| Field | Type | Notes |
|-------|------|-------|
| `Id` | Guid | Unique identifier |
| `BlastId` | Guid | Parent blast |
| `Name` | string | e.g., "H-01" |
| `Position` | (X, Y, Z) | Collar position in local coordinates |
| `Direction` | double | Azimuth in degrees (0–360) |
| `Inclination` | double | Dip angle in degrees (-90 to 90, 0 = vertical) |
| `Status` | enum | Planned → Charged → Ready |

## Invariants & Business Rules

1. **Blast must have holes** to be fired.
2. **All holes must be in Ready status** before firing (stricter than original spec — bonus feature).
3. **Cannot charge a hole twice** — raises `InvalidOperationException`.
4. **Cannot mark a hole ready if not Charged** — raises `InvalidOperationException`.
5. **Cannot fire an already-fired blast** — raises `InvalidOperationException`.
6. **Direction must be 0–360 degrees**, Inclination must be -90 to 90 degrees.
7. **Optimistic concurrency:** Version mismatch raises `InvalidOperationException`.

## Event Sourcing Design

### Event Store

The event store (`InMemoryEventStore`) is the single source of truth. It maintains:

1. **Event Streams**: Dictionary of aggregate IDs to ordered event lists
2. **Versioning**: Each event has a version = its position in the stream
3. **Optimistic Concurrency**: Expected version must match current version when appending
4. **Event Subscriptions**: Observers can subscribe for read model updates

**Why in-memory?**
- Simpler implementation
- No database setup required
- Suitable for this exercise
- Demonstrates the pattern clearly
- Can be replaced with persistent store (e.g., SQL, EventStoreDB)

### Events

All events inherit from `IDomainEvent`:

```csharp
public interface IDomainEvent
{
    Guid AggregateId { get; }
    long Version { get; set; }
    DateTimeOffset Timestamp { get; set; }
    string EventType { get; }
}
```

**Blast Events:**
- `BlastCreated`: New blast created with name
- `BlastLoaded`: Blast transitioned to Loaded status
- `BlastFired`: Blast fired with timestamp

**Hole Events:**
- `HoleAdded`: Hole added to blast
- `HoleCharged`: Hole marked as charged
- `HoleMarkedReady`: Hole marked as ready (bonus feature)

## CQRS Implementation

### Commands (Write Side)

Commands are validated and executed by handlers:

```csharp
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(TCommand command);
}
```

**Command Handlers:**
1. Load aggregate from event stream
2. Apply command (validate rules, raise events)
3. Persist events via event store
4. Return success/error

**Handlers Implemented:**
- `CreateBlastCommandHandler`
- `AddHoleCommandHandler`
- `ChargeHoleCommandHandler`
- `MarkHoleReadyCommandHandler`
- `FireBlastCommandHandler`

### Queries (Read Side)

Queries read from projections or event replay:

```csharp
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery
{
    Task<TResult?> HandleAsync(TQuery query);
}
```

**Query Handlers:**
- `GetBlastQueryHandler`: Returns blast state from read model
- `GetBlastHistoryQueryHandler`: Returns raw event log

## Read Model / Projection (Bonus Feature)

### Trade-off: Replay vs. Read Model

**Option 1: Event Replay**
- Pros: Always consistent, no extra storage
- Cons: Slow for large event streams, repeated work

**Option 2: Read Model (Projection)**
- Pros: O(1) fast reads, shared by multiple queries
- Cons: Eventual consistency, extra memory

**Our Implementation:**
- `GetBlast` uses the read model (fast)
- `GetBlastHistory` replays events directly
- The read model (`BlastReadModel`) subscribes to all events in real-time

**BlastReadModel:**
- Maintains in-memory cache of blast state
- Updates asynchronously as events arrive
- Handles all event types (BlastCreated, HoleAdded, etc.)
- Thread-safe via locking

**When to use:**
- Read model: High-traffic queries, eventual consistency acceptable
- Event replay: Low-traffic queries, strong consistency required, temporal queries

## Testing the API

### Example Flow

```bash
# 1. Create a blast
curl -X POST https://localhost:5001/blasts \
  -H "Content-Type: application/json" \
  -d '{"name": "B-042"}'
# Response: { "success": true, "data": { "id": "..." } }

# 2. Add holes
curl -X POST https://localhost:5001/blasts/{blastId}/holes \
  -H "Content-Type: application/json" \
  -d '{
    "name": "H-01",
    "x": 100, "y": 200, "z": -50,
    "direction": 45, "inclination": -15
  }'

# 3. Charge hole
curl -X PUT https://localhost:5001/blasts/{blastId}/holes/{holeId}/charge

# 4. Mark hole ready
curl -X PUT https://localhost:5001/blasts/{blastId}/holes/{holeId}/ready

# 5. Fire blast
curl -X POST https://localhost:5001/blasts/{blastId}/fire

# 6. Get final state
curl https://localhost:5001/blasts/{blastId}

# 7. Get event history
curl https://localhost:5001/blasts/{blastId}/history
```

## Error Handling

| Status | Condition | Example |
|--------|-----------|---------|
| `400` | Business rule violated | Cannot charge already-charged hole |
| `404` | Aggregate not found | Blast ID doesn't exist |
| `409` | Concurrency conflict | Version mismatch on append |

## Project Structure

```
BlastManagementAPI/
├── Domain/                          # Domain Layer
│   ├── Aggregates/
│   │   ├── Blast.cs                 # Aggregate root
│   │   └── Hole.cs                  # Child entity
│   ├── Events/
│   │   ├── IDomainEvent.cs          # Event base interface
│   │   ├── BlastEvents.cs           # Blast-related events
│   │   └── HoleEvents.cs            # Hole-related events
│   ├── Enums.cs                     # BlastStatus, HoleStatus
│   └── Position.cs                  # Value object
│
├── Application/                     # Application Layer (CQRS)
│   ├── Commands/
│   │   ├── Commands.cs              # Command definitions
│   │   └── CommandHandlers.cs       # Command handlers
│   └── Queries/
│       ├── Queries.cs               # Query definitions
│       └── QueryHandlers.cs         # Query handlers
│
├── Infrastructure/                  # Infrastructure Layer
│   ├── EventStore/
│   │   └── IEventStore.cs           # Event store interface & in-memory implementation
│   └── Projections/
│       └── BlastReadModel.cs        # Read model projection (bonus)
│
├── API/                             # API Layer (Minimal API)
│   ├── Endpoints/
│   │   └── BlastEndpoints.cs        # HTTP endpoint mappings
│   └── DTOs/
│       └── Requests.cs              # Request/response DTOs
│
├── Program.cs                       # Application entry point
└── BlastManagementAPI.csproj        # Project file
```

## Key Design Decisions

### 1. **No Database Required**
All state lives in memory. This demonstrates the pattern clearly without infrastructure overhead.

### 2. **No External Frameworks**
- No MediatR for CQRS dispatch
- No aggregate frameworks
- Pattern implemented from scratch

### 3. **Minimal API** (not Controllers)
ASP.NET Core Minimal APIs provide a lightweight, focused HTTP layer.

### 4. **Immutable State**
- No property setters
- State derived solely from events
- Type-safe via required init-only properties

### 5. **Optimistic Concurrency**
Version tracking allows multiple clients to work concurrently without locking.

### 6. **Event Subscriptions**
Event store supports observers for building read models and other event-driven features.

### 7. **Invariant Enforcement**
Business rules validated in aggregate methods, not in handlers or endpoints.

## Future Enhancements

1. **Persistent Event Store**: Replace in-memory with SQL Server, EventStoreDB, or similar
2. **Snapshots**: Cache aggregate state at intervals to speed up replay
3. **Event Versioning**: Support schema evolution for events
4. **Temporal Queries**: "What was the state at time T?"
5. **Multiple Read Models**: Different projections for different use cases
6. **Event Publishing**: Publish events to a message bus for cross-service communication
7. **Unit & Integration Tests**: Test aggregates, handlers, and endpoints
8. **Compensation**: Implement compensating transactions for distributed systems
9. **CQRS Separation**: Separate read and write databases
10. **Event Encryption**: Encrypt sensitive event data

## Summary

This implementation demonstrates:

✅ **Event Sourcing**: Events are the source of truth; state is derived via replay  
✅ **CQRS**: Commands (write) and queries (read) are separate  
✅ **Aggregates**: Blast aggregate enforces invariants and raises events  
✅ **Optimistic Concurrency**: Version-based conflict detection  
✅ **Event Subscriptions**: Read model updated asynchronously  
✅ **Clean Architecture**: Layered design with clear separation of concerns  
✅ **No External Frameworks**: Pattern implemented from first principles  
✅ **Bonus Feature**: MarkHoleReady command and stricter invariants  
✅ **Bonus Feature**: Read model projection with trade-off explanation  

---

**Author**: AYADI Amir
**Framework**: .NET 8.0 with Minimal APIs  
**License**: MIT
