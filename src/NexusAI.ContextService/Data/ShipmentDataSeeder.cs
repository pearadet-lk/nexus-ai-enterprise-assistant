using Microsoft.EntityFrameworkCore;
using NexusAI.ContextService.Data;
using NexusAI.ContextService.Entities;

namespace NexusAI.ContextService.Data;

public static class ShipmentDataSeeder
{
    public static async Task SeedAsync(NexusDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Shipments.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Shipments.AddRange(
            new Shipment
            {
                ShipmentId = "SHP-1001",
                Origin = "Thailand",
                Destination = "Singapore",
                Status = "Delayed",
                DelayDays = 4,
                Eta = now.AddDays(2),
                CreatedAt = now
            },
            new Shipment
            {
                ShipmentId = "SHP-1002",
                Origin = "Thailand",
                Destination = "Japan",
                Status = "Delayed",
                DelayDays = 2,
                Eta = now.AddDays(1),
                CreatedAt = now
            },
            new Shipment
            {
                ShipmentId = "SHP-1003",
                Origin = "Singapore",
                Destination = "Australia",
                Status = "On Time",
                DelayDays = 0,
                Eta = now.AddDays(3),
                CreatedAt = now
            },
            new Shipment
            {
                ShipmentId = "SHP-1004",
                Origin = "Thailand",
                Destination = "Germany",
                Status = "Delayed",
                DelayDays = 5,
                Eta = now.AddDays(4),
                CreatedAt = now
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}
