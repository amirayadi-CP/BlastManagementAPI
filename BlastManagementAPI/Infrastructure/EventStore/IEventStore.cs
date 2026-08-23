using BlastManagementAPI.Domain.Events;

namespace BlastManagementAPI.Infrastructure.EventStore;

/// <summary>
/// In-memory event store implementation.
/// 
/// DESIGN EXPLANATION:
/// This event store uses a Dictionary keyed by aggregate ID to store ordered event streams.
/// Each stream maintains an immutable sequence of versioned events.
/// 
/// Key design decisions:
/// 1. Version numbering: Starts at 1 for each aggregate. Version represents the sequential order.
/// 2. Optimistic concurrency: Expected version must match the actual stream version when appending.
/// 3. Single source of truth: Events are the only persisted data; state is derived via replay.
/// 4. Immutability: Events are never updated or deleted, only appended.
/// 5. Event subscription: Observers can subscribe to events for building read models (projections).
/// 
/// This design ensures event sourcing semantics:
/// - Complete audit trail of all state changes
/// - Ability to replay history to any point in time
/// - Temporal queries (what was the state at time T?)
/// - Multiple read models from a single event stream
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends events for an aggregate. Throws if expected version does not match.
    /// </summary>
    Task AppendEventsAsync(Guid aggregateId, IEnumerable<IDomainEvent> events, long expectedVersion);

    /// <summary>
    /// Loads all events for an aggregate, in order.
    /// </summary>
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId);

    /// <summary>
    /// Subscribe to all events for building read models.
    /// </summary>
    void Subscribe(Func<IDomainEvent, Task> handler);
}

public class InMemoryEventStore : IEventStore
{
    // Key: AggregateId, Value: ordered list of events
    private readonly Dictionary<Guid, List<IDomainEvent>> _eventStreams = new();
    private readonly List<Func<IDomainEvent, Task>> _subscribers = new();
    private readonly object _lock = new();

    public async Task AppendEventsAsync(Guid aggregateId, IEnumerable<IDomainEvent> events, long expectedVersion)
    {
        lock (_lock)
        {
            // Get or create the stream
            if (!_eventStreams.ContainsKey(aggregateId))
            {
                _eventStreams[aggregateId] = new();
            }

            var stream = _eventStreams[aggregateId];

            // Optimistic concurrency check
            long currentVersion = stream.Count == 0 ? 0 : stream[^1].Version;
            if (currentVersion != expectedVersion)
            {
                throw new InvalidOperationException(
                    $"Concurrency conflict for aggregate {aggregateId}: expected version {expectedVersion}, but current version is {currentVersion}.");
            }

            // Append events
            foreach (var @event in events)
            {
                stream.Add(@event);
            }
        }

        // Notify subscribers asynchronously (outside the lock to avoid deadlocks)
        foreach (var @event in events)
        {
            foreach (var subscriber in _subscribers)
            {
                await subscriber(@event);
            }
        }
    }

    public Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId)
    {
        lock (_lock)
        {
            if (!_eventStreams.ContainsKey(aggregateId))
            {
                return Task.FromResult(Enumerable.Empty<IDomainEvent>());
            }

            // Return a copy to prevent external modifications
            return Task.FromResult<IEnumerable<IDomainEvent>>(_eventStreams[aggregateId].ToList());
        }
    }

    public void Subscribe(Func<IDomainEvent, Task> handler)
    {
        _subscribers.Add(handler);
    }
}
