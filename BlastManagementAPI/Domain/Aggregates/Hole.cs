namespace BlastManagementAPI.Domain.Aggregates;

/// <summary>
/// Represents a hole in a blast. This is a child entity within the Blast aggregate.
/// State is immutable and derived solely from events.
/// </summary>
public class Hole
{
    public Guid Id { get; private set; }
    public Guid BlastId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Position Position { get; private set; } = null!;
    public double Direction { get; private set; }
    public double Inclination { get; private set; }
    public HoleStatus Status { get; private set; }

    public Hole() { }

    public Hole(Guid id, Guid blastId, string name, Position position, double direction, double inclination)
    {
        Id = id;
        BlastId = blastId;
        Name = name;
        Position = position;
        Direction = direction;
        Inclination = inclination;
        Status = HoleStatus.Planned;
    }

    public void ApplyHoleCharged()
    {
        if (Status == HoleStatus.Ready || Status == HoleStatus.Charged)
        {
            throw new InvalidOperationException($"Cannot charge hole {Name}: already in {Status} status.");
        }
        Status = HoleStatus.Charged;
    }

    public void ApplyHoleMarkedReady()
    {
        if (Status != HoleStatus.Charged)
        {
            throw new InvalidOperationException($"Cannot mark hole {Name} as ready: it must be Charged first.");
        }
        Status = HoleStatus.Ready;
    }
}
