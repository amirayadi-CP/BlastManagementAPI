using BlastManagementAPI.API.DTOs;
using BlastManagementAPI.Application.Commands;
using BlastManagementAPI.Application.Queries;
using BlastManagementAPI.Domain;
using BlastManagementAPI.Infrastructure.EventStore;
using BlastManagementAPI.Infrastructure.Projections;

namespace BlastManagementAPI.API.Endpoints;

public static class BlastEndpoints
{
    public static void MapBlastEndpoints(this WebApplication app)
    {
    app.MapPost("/blasts", CreateBlast)
        .WithName("CreateBlast")
        .WithDescription("Create a new blast");

    app.MapPost("/blasts/{blastId}/holes", AddHole)
        .WithName("AddHole")
        .WithDescription("Add a hole to a blast");

    app.MapPut("/blasts/{blastId}/holes/{holeId}/charge", ChargeHole)
        .WithName("ChargeHole")
        .WithDescription("Charge a hole");

    app.MapPut("/blasts/{blastId}/holes/{holeId}/ready", MarkHoleReady)
        .WithName("MarkHoleReady")
        .WithDescription("Mark a hole as ready");

    app.MapPost("/blasts/{blastId}/fire", FireBlast)
        .WithName("FireBlast")
        .WithDescription("Fire a blast");

    app.MapGet("/blasts/{blastId}", GetBlast)
        .WithName("GetBlast")
        .WithDescription("Get blast details");

    app.MapGet("/blasts/{blastId}/history", GetBlastHistory)
        .WithName("GetBlastHistory")
        .WithDescription("Get blast event history");
}

    private static async Task<IResult> CreateBlast(
        CreateBlastRequest request,
        IEventStore eventStore,
        HttpContext context)
    {
        var handler = new CreateBlastCommandHandler(eventStore);
        var command = new CreateBlastCommand { Name = request.Name };
        var result = await handler.HandleAsync(command);

        if (!result.Success)
            return Results.BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = result.Message
            });

        return Results.Created($"/blasts/{result.AggregateId}", new ApiResponse<object>
        {
            Success = true,
            Message = result.Message,
            Data = new { id = result.AggregateId }
        });
    }

    private static async Task<IResult> AddHole(
        Guid blastId,
        AddHoleRequest request,
        IEventStore eventStore,
        HttpContext context)
    {
        var handler = new AddHoleCommandHandler(eventStore);
        var command = new AddHoleCommand
        {
            BlastId = blastId,
            Name = request.Name,
            X = request.X,
            Y = request.Y,
            Z = request.Z,
            Direction = request.Direction,
            Inclination = request.Inclination
        };

        var result = await handler.HandleAsync(command);

        if (!result.Success)
            return Results.BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = result.Message
            });

        return Results.Ok(new ApiResponse<object>
        {
            Success = true,
            Message = result.Message
        });
    }

    private static async Task<IResult> ChargeHole(
        Guid blastId,
        Guid holeId,
        IEventStore eventStore)
    {
        var handler = new ChargeHoleCommandHandler(eventStore);
        var command = new ChargeHoleCommand { BlastId = blastId, HoleId = holeId };
        var result = await handler.HandleAsync(command);

        if (!result.Success)
        {
            return result.Message?.Contains("not found") ?? false
                ? Results.NotFound(new ApiResponse<object> { Success = false, Message = result.Message })
                : Results.BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });
        }

        return Results.Ok(new ApiResponse<object>
        {
            Success = true,
            Message = result.Message
        });
    }

    private static async Task<IResult> MarkHoleReady(
        Guid blastId,
        Guid holeId,
        IEventStore eventStore)
    {
        var handler = new MarkHoleReadyCommandHandler(eventStore);
        var command = new MarkHoleReadyCommand { BlastId = blastId, HoleId = holeId };
        var result = await handler.HandleAsync(command);

        if (!result.Success)
        {
            return result.Message?.Contains("not found") ?? false
                ? Results.NotFound(new ApiResponse<object> { Success = false, Message = result.Message })
                : Results.BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });
        }

        return Results.Ok(new ApiResponse<object>
        {
            Success = true,
            Message = result.Message
        });
    }

    private static async Task<IResult> FireBlast(
        Guid blastId,
        IEventStore eventStore)
    {
        var handler = new FireBlastCommandHandler(eventStore);
        var command = new FireBlastCommand { BlastId = blastId };
        var result = await handler.HandleAsync(command);

        if (!result.Success)
        {
            return result.Message?.Contains("not found") ?? false
                ? Results.NotFound(new ApiResponse<object> { Success = false, Message = result.Message })
                : Results.BadRequest(new ApiResponse<object> { Success = false, Message = result.Message });
        }

        return Results.Ok(new ApiResponse<object>
        {
            Success = true,
            Message = result.Message
        });
    }

    private static async Task<IResult> GetBlast(
        Guid blastId,
        IEventStore eventStore,
        BlastReadModel readModel)
    {
        var handler = new GetBlastQueryHandler(eventStore, readModel);
        var query = new GetBlastQuery { BlastId = blastId };
        var result = await handler.HandleAsync(query);

        if (result == null)
            return Results.NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = $"Blast {blastId} not found."
            });

        return Results.Ok(new ApiResponse<object>
        {
            Success = true,
            Data = result
        });
    }

    private static async Task<IResult> GetBlastHistory(
        Guid blastId,
        IEventStore eventStore)
    {
        var handler = new GetBlastHistoryQueryHandler(eventStore);
        var query = new GetBlastHistoryQuery { BlastId = blastId };
        var result = await handler.HandleAsync(query);

        if (!result.Any())
            return Results.NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = $"Blast {blastId} not found."
            });

        return Results.Ok(new ApiResponse<object>
        {
            Success = true,
            Data = result
        });
    }
}
