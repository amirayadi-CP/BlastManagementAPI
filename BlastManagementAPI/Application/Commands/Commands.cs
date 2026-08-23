namespace BlastManagementAPI.Application.Commands;

/// <summary>
/// Base interface for commands.
/// Commands represent an intent to change state.
/// </summary>
public interface ICommand { }

public class CreateBlastCommand : ICommand
{
    public required string Name { get; init; }
}

public class AddHoleCommand : ICommand
{
    public required Guid BlastId { get; init; }
    public required string Name { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
    public required double Direction { get; init; }
    public required double Inclination { get; init; }
}

public class ChargeHoleCommand : ICommand
{
    public required Guid BlastId { get; init; }
    public required Guid HoleId { get; init; }
}

public class MarkHoleReadyCommand : ICommand
{
    public required Guid BlastId { get; init; }
    public required Guid HoleId { get; init; }
}

public class FireBlastCommand : ICommand
{
    public required Guid BlastId { get; init; }
}
