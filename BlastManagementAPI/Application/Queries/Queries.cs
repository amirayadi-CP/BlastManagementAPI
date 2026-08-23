using BlastManagementAPI.Domain.Events;

namespace BlastManagementAPI.Application.Queries;

/// <summary>
/// Base interface for queries.
/// Queries return data without modifying state.
/// </summary>
public interface IQuery { }

public class GetBlastQuery : IQuery
{
    public required Guid BlastId { get; init; }
}

public class GetBlastHistoryQuery : IQuery
{
    public required Guid BlastId { get; init; }
}
