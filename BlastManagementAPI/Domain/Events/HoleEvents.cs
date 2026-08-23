namespace BlastManagementAPI.Domain.Events;

/// <summary>
/// Raised when a hole is added to a blast.
/// </summary>
public class HoleAdded : IDomainEvent
{
    public Guid AggregateId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(HoleAdded);

    public required Guid HoleId { get; init; }
    public required string Name { get; init; }
    public required Position Position { get; init; }
    public required double Direction { get; init; }
    public required double Inclination { get; init; }

    public HoleAdded() { }

    public HoleAdded(Guid blastId, Guid holeId, string name, Position position, double direction, double inclination)
    {
        AggregateId = blastId;
        HoleId = holeId;
        Name = name;
        Position = position;
        Direction = direction;
        Inclination = inclination;
    }
}

/// <summary>
/// Raised when a hole is charged.
/// </summary>
public class HoleCharged : IDomainEvent
{
    public Guid AggregateId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(HoleCharged);

    public required Guid HoleId { get; init; }

    public HoleCharged() { }

    public HoleCharged(Guid blastId, Guid holeId)
    {
        AggregateId = blastId;
        HoleId = holeId;
    }
}

/// <summary>
/// Raised when a hole is marked as ready.
/// </summary>
public class HoleMarkedReady : IDomainEvent
{
    public Guid AggregateId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(HoleMarkedReady);

    public required Guid HoleId { get; init; }

    public HoleMarkedReady() { }

    public HoleMarkedReady(Guid blastId, Guid holeId)
    {
        AggregateId = blastId;
        HoleId = holeId;
    }
}
