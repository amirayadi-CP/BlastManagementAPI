using BlastManagementAPI.Domain.Aggregates;
using BlastManagementAPI.Infrastructure.EventStore;
using BlastManagementAPI.Infrastructure.Projections;

namespace BlastManagementAPI.Application.Queries;

/// <summary>
/// Base interface for query handlers.
/// Queries read from projections or by replaying events, without modifying state.
/// </summary>
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery
{
    Task<TResult?> HandleAsync(TQuery query);
}

public class GetBlastQueryHandler : IQueryHandler<GetBlastQuery, BlastDto?>
{
    private readonly IEventStore _eventStore;
    private readonly BlastReadModel _readModel;

    public GetBlastQueryHandler(IEventStore eventStore, BlastReadModel readModel)
    {
        _eventStore = eventStore;
        _readModel = readModel;
    }

    public async Task<BlastDto?> HandleAsync(GetBlastQuery query)
    {
        // TRADE-OFF EXPLANATION:
        // This query handler demonstrates the read model pattern (bonus feature).
        // 
        // Option 1 (Replaying): Load all events and replay to rebuild state.
        //   - Pros: No separate storage, always consistent with events
        //   - Cons: Slow for aggregates with many events, repeated replays wasteful
        // 
        // Option 2 (Read Model): Query a pre-built projection updated in real-time.
        //   - Pros: Fast reads, scalable, eventual consistency
        //   - Cons: Separate data structure, requires event subscription
        //
        // We use Option 2: GetBlast queries the read model (which is subscribed to all events).
        // GetBlastHistory still replays events directly (option 1).
        // This demonstrates both patterns.

        return _readModel.GetBlast(query.BlastId);
    }
}

public class GetBlastHistoryQueryHandler : IQueryHandler<GetBlastHistoryQuery, List<EventDto>?>
{
    private readonly IEventStore _eventStore;

    public GetBlastHistoryQueryHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<List<EventDto>?> HandleAsync(GetBlastHistoryQuery query)
    {
        var events = await _eventStore.GetEventsAsync(query.BlastId);

        if (!events.Any())
            return null;

        return events
            .OrderBy(e => e.Version)
            .Select(e => new EventDto
            {
                Version = e.Version,
                EventType = e.EventType,
                Timestamp = e.Timestamp,
                Data = SerializeEvent(e)
            })
            .ToList();
    }

    private static object SerializeEvent(Domain.Events.IDomainEvent @event)
    {
        return @event switch
        {
            Domain.Events.BlastCreated e => new { e.Name },
            Domain.Events.BlastLoaded _ => new { },
            Domain.Events.BlastFired e => new { e.DateBlasted },
            Domain.Events.HoleAdded e => new
            {
                e.HoleId,
                e.Name,
                Position = new { e.Position.X, e.Position.Y, e.Position.Z },
                e.Direction,
                e.Inclination
            },
            Domain.Events.HoleCharged e => new { e.HoleId },
            Domain.Events.HoleMarkedReady e => new { e.HoleId },
            _ => new { }
        };
    }
}

public record BlastDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? DateBlasted { get; init; }
    public required List<HoleDto> Holes { get; init; }
}

public record HoleDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
    public required double Direction { get; init; }
    public required double Inclination { get; init; }
    public required string Status { get; init; }
}

public record EventDto
{
    public required long Version { get; init; }
    public required string EventType { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required object Data { get; init; }
}
