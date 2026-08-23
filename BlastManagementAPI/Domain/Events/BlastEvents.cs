namespace BlastManagementAPI.Domain.Events;

/// <summary>
/// Raised when a new blast is created.
/// </summary>
public class BlastCreated : IDomainEvent
{
    public Guid AggregateId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(BlastCreated);

    public required string Name { get; init; }

    public BlastCreated() { }

    public BlastCreated(Guid blastId, string name)
    {
        AggregateId = blastId;
        Name = name;
    }
}

/// <summary>
/// Raised when the blast transitions to Loaded status.
/// This occurs when holes are being added to the blast.
/// </summary>
public class BlastLoaded : IDomainEvent
{
    public Guid AggregateId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(BlastLoaded);

    public BlastLoaded() { }

    public BlastLoaded(Guid blastId)
    {
        AggregateId = blastId;
    }
}

/// <summary>
/// Raised when the blast is fired.
/// </summary>
public class BlastFired : IDomainEvent
{
    public Guid AggregateId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType => nameof(BlastFired);

    public required DateTimeOffset DateBlasted { get; init; }

    public BlastFired() { }

    public BlastFired(Guid blastId, DateTimeOffset dateBlasted)
    {
        AggregateId = blastId;
        DateBlasted = dateBlasted;
    }
}
