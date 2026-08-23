using BlastManagementAPI.Application.Queries;
using BlastManagementAPI.Domain;
using BlastManagementAPI.Domain.Aggregates;
using BlastManagementAPI.Domain.Events;

namespace BlastManagementAPI.Infrastructure.Projections;

/// <summary>
/// Read model for Blast aggregates.
/// This projection is built by subscribing to domain events and maintaining an in-memory cache.
/// 
/// TRADE-OFF (bonus feature):
/// This demonstrates the benefit of read models:
/// - GetBlast query is O(1) lookup instead of O(n) event replay
/// - Multiple queries can share the same projection without re-processing events
/// - Eventual consistency: the projection updates asynchronously as events arrive
/// 
/// Trade-off: Requires additional memory to store the projection, and consistency is eventual (not immediate).
/// </summary>
public class BlastReadModel
{
    private readonly Dictionary<Guid, BlastDto> _blasts = new();
    private readonly object _lock = new();

    public void Handle(IDomainEvent @event)
    {
        lock (_lock)
        {
            var aggregateId = @event.AggregateId;

            switch (@event)
            {
                case BlastCreated e:
                    _blasts[aggregateId] = new BlastDto
                    {
                        Id = e.AggregateId,
                        Name = e.Name,
                        Status = BlastStatus.Planned.ToString(),
                        Holes = new()
                    };
                    break;

                case BlastLoaded e:
                    if (_blasts.TryGetValue(aggregateId, out var blast))
                    {
                        var updated = blast with { Status = BlastStatus.Loaded.ToString() };
                        _blasts[aggregateId] = updated;
                    }
                    break;

                case HoleAdded e:
                    if (_blasts.TryGetValue(aggregateId, out var blastWithHole))
                    {
                        var hole = new HoleDto
                        {
                            Id = e.HoleId,
                            Name = e.Name,
                            X = e.Position.X,
                            Y = e.Position.Y,
                            Z = e.Position.Z,
                            Direction = e.Direction,
                            Inclination = e.Inclination,
                            Status = HoleStatus.Planned.ToString()
                        };

                        var updatedHoles = new List<HoleDto>(blastWithHole.Holes) { hole };
                        var updated = blastWithHole with { Holes = updatedHoles };
                        _blasts[aggregateId] = updated;
                    }
                    break;

                case HoleCharged e:
                    if (_blasts.TryGetValue(aggregateId, out var blastWithChargedHole))
                    {
                        var updatedHoles = blastWithChargedHole.Holes
                            .Select(h => h.Id == e.HoleId
                                ? h with { Status = HoleStatus.Charged.ToString() }
                                : h)
                            .ToList();

                        var updated = blastWithChargedHole with { Holes = updatedHoles };
                        _blasts[aggregateId] = updated;
                    }
                    break;

                case HoleMarkedReady e:
                    if (_blasts.TryGetValue(aggregateId, out var blastWithReadyHole))
                    {
                        var updatedHoles = blastWithReadyHole.Holes
                            .Select(h => h.Id == e.HoleId
                                ? h with { Status = HoleStatus.Ready.ToString() }
                                : h)
                            .ToList();

                        var updated = blastWithReadyHole with { Holes = updatedHoles };
                        _blasts[aggregateId] = updated;
                    }
                    break;

                case BlastFired e:
                    if (_blasts.TryGetValue(aggregateId, out var firedBlast))
                    {
                        var updated = firedBlast with
                        {
                            Status = BlastStatus.Blasted.ToString(),
                            DateBlasted = e.DateBlasted
                        };
                        _blasts[aggregateId] = updated;
                    }
                    break;
            }
        }
    }

    public BlastDto? GetBlast(Guid blastId)
    {
        lock (_lock)
        {
            _blasts.TryGetValue(blastId, out var blast);
            return blast;
        }
    }
}
