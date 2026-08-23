namespace BlastManagementAPI.Domain.Events;

/// <summary>
/// Base interface for all domain events.
/// Domain events represent something that has happened in the domain,
/// expressed in past tense (e.g., BlastCreated, HoleCharged).
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier of the aggregate this event belongs to.
    /// </summary>
    Guid AggregateId { get; }

    /// <summary>
    /// The version (sequence number) of this event in the stream.
    /// </summary>
    long Version { get; set; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Event type name for deserialization.
    /// </summary>
    string EventType { get; }
}
