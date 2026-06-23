using Microsoft.EntityFrameworkCore;
using NexusAI.ContextService.Entities;

namespace NexusAI.ContextService.Data;

public sealed class NexusDbContext(DbContextOptions<NexusDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<ConversationMemory> ConversationMemories => Set<ConversationMemory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(256);
            entity.HasIndex(x => x.UserId);
            entity.HasMany(x => x.Messages)
                .WithOne(x => x.Conversation)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => x.ConversationId);
        });

        modelBuilder.Entity<ToolExecution>(entity =>
        {
            entity.ToTable("ToolExecutions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ToolName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(128);
            entity.Property(x => x.Action).HasMaxLength(256);
            entity.Property(x => x.Cost).HasPrecision(18, 6);
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.ToTable("Shipments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ShipmentId).IsUnique();
            entity.Property(x => x.ShipmentId).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Origin).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Destination).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<ConversationMemory>(entity =>
        {
            entity.ToTable("ConversationMemories");
            entity.HasKey(x => x.ConversationId);
            entity.Property(x => x.Summary).HasMaxLength(4000);
            entity.Property(x => x.PreferencesJson).HasMaxLength(2000);
            entity.HasOne(x => x.Conversation)
                .WithOne()
                .HasForeignKey<ConversationMemory>(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
