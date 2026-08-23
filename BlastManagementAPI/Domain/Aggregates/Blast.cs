using BlastManagementAPI.Domain.Events;

namespace BlastManagementAPI.Domain.Aggregates;

/// <summary>
/// Blast aggregate root. Represents a blast operation with its holes.
/// The aggregate is event-sourced: state is derived exclusively by replaying events.
/// No setters for properties — state mutations happen only through command methods that raise events.
/// </summary>
public class Blast
{
    // Immutable state — no public setters
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset? DateBlasted { get; private set; }
    public BlastStatus Status { get; private set; }
    public IReadOnlyList<Hole> Holes => _holes.AsReadOnly();

    // Version tracking for optimistic concurrency
    public long Version { get; private set; }

    // Uncommitted events that will be persisted
    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    private readonly List<Hole> _holes = new();
    private readonly List<IDomainEvent> _uncommittedEvents = new();

    public Blast() { }

    /// <summary>
    /// Creates a new blast (factory method).
    /// This raises a BlastCreated event but does NOT persist it — the caller must persist via the event store.
    /// </summary>
    public static Blast CreateBlast(Guid blastId, string name)
    {
        var blast = new Blast
        {
            Id = blastId,
            Name = name,
            Status = BlastStatus.Planned,
            Version = 1
        };

        var @event = new BlastCreated
        {
            AggregateId = blastId,
            Name = name,
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow
        };

        blast._uncommittedEvents.Add(@event);
        return blast;
    }

    /// <summary>
    /// Adds a hole to the blast.
    /// Raises HoleAdded event.
    /// </summary>
    public void AddHole(Guid holeId, string name, Position position, double direction, double inclination)
    {
        if (Status == BlastStatus.Blasted)
        {
            throw new InvalidOperationException("Cannot add holes to a blasted blast.");
        }

        // Validate coordinates
        if (direction < 0 || direction > 360)
            throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be between 0 and 360 degrees.");
        if (inclination < -90 || inclination > 90)
            throw new ArgumentOutOfRangeException(nameof(inclination), "Inclination must be between -90 and 90 degrees.");

        // Transition to Loaded status if not already
        if (Status == BlastStatus.Planned)
        {
            Status = BlastStatus.Loaded;
            var loadedEvent = new BlastLoaded
            {
                AggregateId = Id,
                Version = Version + 1,
                Timestamp = DateTimeOffset.UtcNow
            };
            _uncommittedEvents.Add(loadedEvent);
            Version++;
        }

        var hole = new Hole(holeId, Id, name, position, direction, inclination);
        _holes.Add(hole);

        var @event = new HoleAdded
        {
            AggregateId = Id,
            HoleId = holeId,
            Name = name,
            Position = position,
            Direction = direction,
            Inclination = inclination,
            Version = Version + 1,
            Timestamp = DateTimeOffset.UtcNow
        };

        _uncommittedEvents.Add(@event);
        Version++;
    }

    /// <summary>
    /// Charges a hole.
    /// Raises HoleCharged event.
    /// </summary>
    public void ChargeHole(Guid holeId)
    {
        var hole = _holes.FirstOrDefault(h => h.Id == holeId)
            ?? throw new InvalidOperationException($"Hole {holeId} not found in blast {Id}.");

        hole.ApplyHoleCharged();

        var @event = new HoleCharged
        {
            AggregateId = Id,
            HoleId = holeId,
            Version = Version + 1,
            Timestamp = DateTimeOffset.UtcNow
        };

        _uncommittedEvents.Add(@event);
        Version++;
    }

    /// <summary>
    /// Marks a hole as ready (Charged → Ready).
    /// Raises HoleMarkedReady event.
    /// </summary>
    public void MarkHoleReady(Guid holeId)
    {
        var hole = _holes.FirstOrDefault(h => h.Id == holeId)
            ?? throw new InvalidOperationException($"Hole {holeId} not found in blast {Id}.");

        hole.ApplyHoleMarkedReady();

        var @event = new HoleMarkedReady
        {
            AggregateId = Id,
            HoleId = holeId,
            Version = Version + 1,
            Timestamp = DateTimeOffset.UtcNow
        };

        _uncommittedEvents.Add(@event);
        Version++;
    }

    /// <summary>
    /// Fires the blast.
    /// All holes must be in Ready status (stricter invariant for bonus requirement).
    /// Raises BlastFired event.
    /// </summary>
    public void FireBlast(DateTimeOffset now)
    {
        if (Status == BlastStatus.Blasted)
        {
            throw new InvalidOperationException("Blast has already been fired.");
        }

        // Stricter invariant: all holes must be Ready
        if (_holes.Count == 0)
        {
            throw new InvalidOperationException("Cannot fire a blast with no holes.");
        }

        var notReadyHoles = _holes.Where(h => h.Status != HoleStatus.Ready).ToList();
        if (notReadyHoles.Any())
        {
            var holeNames = string.Join(", ", notReadyHoles.Select(h => $"{h.Name} ({h.Status})"));
            throw new InvalidOperationException($"Cannot fire blast: holes not ready: {holeNames}");
        }

        Status = BlastStatus.Blasted;
        DateBlasted = now;

        var @event = new BlastFired
        {
            AggregateId = Id,
            DateBlasted = now,
            Version = Version + 1,
            Timestamp = now
        };

        _uncommittedEvents.Add(@event);
        Version++;
    }

    /// <summary>
    /// Replays an event to rebuild state.
    /// This is called by the event store when loading a blast from history.
    /// </summary>
    public void ApplyEvent(IDomainEvent @event)
    {
        switch (@event)
        {
            case BlastCreated e:
                Id = e.AggregateId;
                Name = e.Name;
                Status = BlastStatus.Planned;
                break;

            case BlastLoaded e:
                Status = BlastStatus.Loaded;
                break;

            case HoleAdded e:
                var hole = new Hole(e.HoleId, e.AggregateId, e.Name, e.Position, e.Direction, e.Inclination);
                _holes.Add(hole);
                break;

            case HoleCharged e:
                var chargedHole = _holes.FirstOrDefault(h => h.Id == e.HoleId);
                if (chargedHole != null)
                    chargedHole.ApplyHoleCharged();
                break;

            case HoleMarkedReady e:
                var readyHole = _holes.FirstOrDefault(h => h.Id == e.HoleId);
                if (readyHole != null)
                    readyHole.ApplyHoleMarkedReady();
                break;

            case BlastFired e:
                Status = BlastStatus.Blasted;
                DateBlasted = e.DateBlasted;
                break;
        }

        Version = @event.Version;
    }

    /// <summary>
    /// Clears uncommitted events after successful persistence.
    /// </summary>
    public void ClearUncommittedEvents()
    {
        _uncommittedEvents.Clear();
    }
}
