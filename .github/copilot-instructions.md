---
description: "Blast Management API project workspace instructions. Coordinates specialized agents for CQRS/Event Sourcing implementation."
---

# Blast Management API — Workspace Instructions

Welcome to the **Blast Management API** workspace! This is a mining blast management system built with CQRS & Event Sourcing on .NET 8.0.

## Quick Overview

- **Stack**: .NET 8.0, Minimal API, Swagger
- **Architecture**: Domain → Application (CQRS) → Infrastructure → API
- **Patterns**: Event Sourcing, Command Query Responsibility Segregation (CQRS), Domain-Driven Design
- **Domain**: Mining blast operations with holes, charging sequences, and firing coordination

## Using the Agents

This workspace has **three specialized agents** designed to work together:

### 1. 🎯 **BlastAPIImplementation** (Start Here)
**Use when**: Planning a new feature, adding commands, queries, events, or fixing bugs

**What it does**:
- Asks clarifying questions about your feature request
- Breaks down implementation across architecture layers
- Shows you a clear plan with all files to modify
- Gets your approval before proceeding
- Hands over to the .NET agent with full context

**Example**: "I want to add a way to mark holes as needing retest"

---

### 2. ⚙️ **BlastManagementAPI** (Specialized .NET Agent)
**Use when**: The BlastAPIImplementation agent hands over, OR for deep architectural questions

**What it does**:
- Implements code following CQRS patterns
- Enforces business rules in aggregates
- Manages event sourcing replay logic
- Writes HTTP endpoints
- Follows coding standards from `blast-api-patterns.instructions.md`

**Example**: "Implement the RemarkHoleRetest command..." (after plan is approved)

---

### 3. 🔍 **Explore** (Read-only Code Search)
**Use when**: You need to understand existing code quickly

**What it does**:
- Searches the codebase for files, patterns, examples
- Explains how existing features work
- Safe read-only operations
- Much faster than manual file browsing

**Example**: "Show me how CreateBlastCommand is implemented"

---

## Recommended Workflows

### Adding a New Feature

```
You ("Add a command to publish blast events")
  ↓
BlastAPIImplementation Agent
  • Asks: "Should this publish to external system or just log?"
  • Plans: "Add PublishBlastCommand, update endpoints"
  • Shows: "Files to modify: Commands.cs, CommandHandlers.cs, BlastEndpoints.cs"
  ↓
You ("Yes, approve")
  ↓
BlastManagementAPI Agent
  • Implements: "Writing PublishBlastCommand..."
  • Follows: CQRS patterns, error handling, HTTP status codes
  • Result: "Feature complete, compiles, tested"
```

### Understanding Existing Code

```
You ("How does the read model projection work?")
  ↓
BlastManagementAPI Agent
  • Explains: Event sourcing replay, eventual consistency trade-offs
  • Shows: BlastReadModel.cs, Program.cs subscription setup
  • Why: "Queries are O(1) fast, eventual consistency acceptable for reads"
```

### Debugging an Issue

```
You ("FireBlast is rejecting valid states")
  ↓
BlastManagementAPI Agent
  • Investigates: Looks at validation in Blast.FireBlast()
  • Identifies: "Checking hole status against wrong enum value"
  • Fixes: Updates the condition, adds test
```

---

## Architecture Layers (Quick Reference)

### 🎯 Domain Layer
**Files**: `Domain/Aggregates/`, `Domain/Events/`, `Domain/`

- **No dependencies** on infrastructure
- **Aggregates** (Blast, Hole) are immutable state machines
- **Events** are the source of truth
- **Invariants** enforced here (throws InvalidOperationException)

**Example**:
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

### 📦 Application Layer
**Files**: `Application/Commands/`, `Application/Queries/`

- **Commands**: Write operations (load → validate → append)
- **Queries**: Read operations (read from cache or replay)
- **Handlers**: Orchestrate domain logic with infrastructure

**Example**:
```csharp
public async Task<CommandResult> HandleAsync(FireBlastCommand cmd)
{
    var blast = new Blast();
    var events = await _eventStore.GetEventsAsync(cmd.BlastId);
    
    foreach (var e in events) blast.ApplyEvent(e);
    
    blast.FireBlast(DateTimeOffset.UtcNow);
    
    await _eventStore.AppendEventsAsync(cmd.BlastId, blast.UncommittedEvents, lastVersion);
    return new CommandResult { Success = true };
}
```

### 🏗️ Infrastructure Layer
**Files**: `Infrastructure/EventStore/`, `Infrastructure/Projections/`

- **Event Store**: Persists immutable event streams
- **Projections**: Cache for fast read models (eventual consistency)
- **Can be replaced** without changing domain or API

### 🌐 API Layer
**Files**: `API/Endpoints/`, `API/DTOs/`

- **HTTP routing** via Minimal APIs
- **Request/Response DTOs**
- **Status codes**: 201 (created), 400 (validation), 404 (not found), 409 (conflict)

---

## CQRS Pattern in This Project

### Write Side (Commands)
```
POST /blasts/{id}/fire
  → FireBlastCommand
  → FireBlastCommandHandler
  → Load aggregate from events
  → Validate & execute: blast.FireBlast()
  → Append BlastFired event
  → Return 200 OK
```

### Read Side (Queries)
```
GET /blasts/{id}
  → GetBlastQuery
  → GetBlastQueryHandler
  → Query BlastReadModel cache
  → Return BlastDto (O(1) fast)
```

---

## Coding Standards

**Always follow** `blast-api-patterns.instructions.md` when writing C# code:

- ✅ Command methods on aggregates (no setters)
- ✅ Events are immutable, past-tense
- ✅ Validation in domain, not endpoints
- ✅ HTTP status 400 for business rule violations
- ✅ Version-based optimistic concurrency

---

## Documentation

Three comprehensive guides in the `context/` folder:

1. **README.md** — API usage, endpoints, examples, quick start
2. **ARCHITECTURE.md** — Deep design patterns, trade-offs, future enhancements
3. **IMPLEMENTATION.md** — Technical details, extension points, testing patterns

Read these to understand the "why" behind the patterns.

---

## Keyboard Shortcuts / Quick Access

**In VS Code**:
- Type `@Agent BlastAPIImplementation` → Start a feature request
- Type `@Agent BlastManagementAPI` → Deep dive on implementation
- Type `@Agent Explore` → Search codebase

**File**: `.github/agents/blast-api-implementation.agent.md` for orchestration rules

---

## Getting Started

### For New Features:
1. Open chat: `@Agent BlastAPIImplementation "I want to..."`
2. Agent will ask clarifying questions
3. Agent shows implementation plan
4. You approve
5. Agent hands to BlastManagementAPI for coding

### For Understanding Code:
1. Open chat: `@Agent Explore "How does [feature] work?"`
2. OR: `@Agent BlastManagementAPI "Explain [pattern]"`

### For Bug Fixes:
1. Open chat: `@Agent BlastManagementAPI "FireBlast is rejecting..."`
2. Agent investigates and fixes

---

## Project Structure

```
BlastManagementAPI/
├── Domain/
│   ├── Aggregates/
│   │   ├── Blast.cs          ← Main aggregate, business logic
│   │   └── Hole.cs           ← Child entity
│   ├── Events/
│   │   ├── IDomainEvent.cs   ← Base interface
│   │   ├── BlastEvents.cs    ← BlastCreated, BlastLoaded, BlastFired
│   │   └── HoleEvents.cs     ← HoleAdded, HoleCharged, HoleMarkedReady
│   ├── Enums.cs              ← BlastStatus, HoleStatus
│   └── Position.cs           ← Value object (X, Y, Z)
│
├── Application/
│   ├── Commands/
│   │   ├── Commands.cs       ← Command definitions
│   │   └── CommandHandlers.cs ← Write-side handlers
│   └── Queries/
│       ├── Queries.cs        ← Query definitions
│       └── QueryHandlers.cs  ← Read-side handlers
│
├── Infrastructure/
│   ├── EventStore/
│   │   └── IEventStore.cs    ← In-memory event persistence
│   └── Projections/
│       └── BlastReadModel.cs ← Read model cache
│
├── API/
│   ├── Endpoints/
│   │   └── BlastEndpoints.cs ← HTTP route mappings
│   └── DTOs/
│       └── Requests.cs       ← Request/response objects
│
├── Program.cs                 ← DI setup, event subscription
└── BlastManagementAPI.csproj  ← .NET project file

.github/
├── agents/
│   ├── blast-api.agent.md            ← Specialized .NET agent
│   └── blast-api-implementation.agent.md ← Implementation orchestrator
└── instructions/
    └── blast-api-patterns.instructions.md ← Coding patterns & rules

context/
├── README.md                  ← API guide & usage
├── ARCHITECTURE.md            ← Design patterns deep-dive
└── IMPLEMENTATION.md          ← Technical details & extension points
```

---

## Troubleshooting

**Q: Agent isn't recognizing my request**  
A: Use explicit language: "Add a command for", "Fix the validation in", "Create a query to"

**Q: I want to skip the planning step**  
A: You can invoke BlastManagementAPI directly, but planning first prevents mistakes

**Q: How do I extend the agent?**  
A: Edit `.github/agents/blast-api-implementation.agent.md` to add patterns or questions

---

## Key Concepts

### Event Sourcing
Events are the only persisted data. State is rebuilt by replaying events in order.
- Complete audit trail
- Temporal queries possible
- Time travel debugging

### CQRS
Commands (write) and Queries (read) are separate with different optimization strategies.
- Commands validate and append events
- Queries read from pre-built cache (eventual consistency)
- Independent scaling

### Aggregates
Consistency boundary. All related data mutates together through command methods.
- Blast aggregate contains holes
- Invariants enforced atomically
- Events raised for state changes

### Optimistic Concurrency
Version numbers detect conflicts when two clients modify the same aggregate.
- No locking
- High throughput
- Occasional conflict resolution needed

---

**Questions?** Ask the agents—they're here to help! 🚀
