using BlastManagementAPI.Domain;
using BlastManagementAPI.Domain.Aggregates;
using BlastManagementAPI.Infrastructure.EventStore;

namespace BlastManagementAPI.Application.Commands;

/// <summary>
/// Base interface for command handlers.
/// Implementations validate business rules, apply commands to aggregates, and persist events.
/// </summary>
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(TCommand command);
}

public class CommandResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid? AggregateId { get; set; }
}

public class CreateBlastCommandHandler : ICommandHandler<CreateBlastCommand>
{
    private readonly IEventStore _eventStore;

    public CreateBlastCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<CommandResult> HandleAsync(CreateBlastCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return new CommandResult { Success = false, Message = "Blast name is required." };

        var blastId = Guid.NewGuid();
        var blast = Blast.CreateBlast(blastId, command.Name);

        try
        {
            await _eventStore.AppendEventsAsync(blastId, blast.UncommittedEvents, 0);
            blast.ClearUncommittedEvents();

            return new CommandResult
            {
                Success = true,
                Message = $"Blast '{command.Name}' created successfully.",
                AggregateId = blastId
            };
        }
        catch (InvalidOperationException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
    }
}

public class AddHoleCommandHandler : ICommandHandler<AddHoleCommand>
{
    private readonly IEventStore _eventStore;

    public AddHoleCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<CommandResult> HandleAsync(AddHoleCommand command)
    {
        // Load the aggregate from the event stream
        var events = await _eventStore.GetEventsAsync(command.BlastId);
        if (!events.Any())
            return new CommandResult { Success = false, Message = $"Blast {command.BlastId} not found." };

        var blast = new Blast();
        var lastVersion = 0L;
        foreach (var @event in events)
        {
            blast.ApplyEvent(@event);
            lastVersion = @event.Version;
        }

        // Apply the command
        try
        {
            var position = new Position(command.X, command.Y, command.Z);
            var holeId = Guid.NewGuid();
            blast.AddHole(holeId, command.Name, position, command.Direction, command.Inclination);

            await _eventStore.AppendEventsAsync(command.BlastId, blast.UncommittedEvents, lastVersion);
            blast.ClearUncommittedEvents();

            return new CommandResult
            {
                Success = true,
                Message = $"Hole '{command.Name}' added to blast '{command.BlastId}'.",
                AggregateId = command.BlastId
            };
        }
        catch (InvalidOperationException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
    }
}

public class ChargeHoleCommandHandler : ICommandHandler<ChargeHoleCommand>
{
    private readonly IEventStore _eventStore;

    public ChargeHoleCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<CommandResult> HandleAsync(ChargeHoleCommand command)
    {
        var events = await _eventStore.GetEventsAsync(command.BlastId);
        if (!events.Any())
            return new CommandResult { Success = false, Message = $"Blast {command.BlastId} not found." };

        var blast = new Blast();
        var lastVersion = 0L;
        foreach (var @event in events)
        {
            blast.ApplyEvent(@event);
            lastVersion = @event.Version;
        }

        try
        {
            blast.ChargeHole(command.HoleId);

            await _eventStore.AppendEventsAsync(command.BlastId, blast.UncommittedEvents, lastVersion);
            blast.ClearUncommittedEvents();

            return new CommandResult
            {
                Success = true,
                Message = $"Hole '{command.HoleId}' charged successfully.",
                AggregateId = command.BlastId
            };
        }
        catch (InvalidOperationException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
    }
}

public class MarkHoleReadyCommandHandler : ICommandHandler<MarkHoleReadyCommand>
{
    private readonly IEventStore _eventStore;

    public MarkHoleReadyCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<CommandResult> HandleAsync(MarkHoleReadyCommand command)
    {
        var events = await _eventStore.GetEventsAsync(command.BlastId);
        if (!events.Any())
            return new CommandResult { Success = false, Message = $"Blast {command.BlastId} not found." };

        var blast = new Blast();
        var lastVersion = 0L;
        foreach (var @event in events)
        {
            blast.ApplyEvent(@event);
            lastVersion = @event.Version;
        }

        try
        {
            blast.MarkHoleReady(command.HoleId);

            await _eventStore.AppendEventsAsync(command.BlastId, blast.UncommittedEvents, lastVersion);
            blast.ClearUncommittedEvents();

            return new CommandResult
            {
                Success = true,
                Message = $"Hole '{command.HoleId}' marked as ready.",
                AggregateId = command.BlastId
            };
        }
        catch (InvalidOperationException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
    }
}

public class FireBlastCommandHandler : ICommandHandler<FireBlastCommand>
{
    private readonly IEventStore _eventStore;

    public FireBlastCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<CommandResult> HandleAsync(FireBlastCommand command)
    {
        var events = await _eventStore.GetEventsAsync(command.BlastId);
        if (!events.Any())
            return new CommandResult { Success = false, Message = $"Blast {command.BlastId} not found." };

        var blast = new Blast();
        var lastVersion = 0L;
        foreach (var @event in events)
        {
            blast.ApplyEvent(@event);
            lastVersion = @event.Version;
        }

        try
        {
            blast.FireBlast(DateTimeOffset.UtcNow);

            await _eventStore.AppendEventsAsync(command.BlastId, blast.UncommittedEvents, lastVersion);
            blast.ClearUncommittedEvents();

            return new CommandResult
            {
                Success = true,
                Message = $"Blast '{command.BlastId}' fired successfully at {blast.DateBlasted}.",
                AggregateId = command.BlastId
            };
        }
        catch (InvalidOperationException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
    }
}
