using NexusAI.SharedKernel.Entities;

namespace NexusAI.ContextService.Entities;

public sealed class Shipment : EntityBase
{
    public required string ShipmentId { get; set; }

    public required string Origin { get; set; }

    public required string Destination { get; set; }

    public required string Status { get; set; }

    public int DelayDays { get; set; }

    public DateTime? Eta { get; set; }
}
