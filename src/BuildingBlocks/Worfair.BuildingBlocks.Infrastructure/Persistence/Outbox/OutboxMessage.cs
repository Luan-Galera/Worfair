namespace Worfair.BuildingBlocks.Infrastructure.Persistence.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using Worfair.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Mensagem do Outbox transacional — uma tabela por módulo
/// ({schema}.outbox_messages, docs/database/03 §10).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? TenantId { get; set; }

    public string Type { get; set; } = default!;

    public string Payload { get; set; } = default!;

    public DateTime OccurredOnUtc { get; set; }

    public DateTime? ProcessedOnUtc { get; set; }

    public string? Error { get; set; }
}

public static class OutboxMessageConfiguration
{
    /// <summary>Mapeia a tabela de outbox no schema do módulo (chamar no OnModelCreating).</summary>
    public static ModelBuilder AddOutboxMessages(this ModelBuilder modelBuilder, string schema)
        => modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages", schema);

            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).HasColumnName("id");

            builder.Property(m => m.TenantId).HasColumnName("tenant_id");

            builder.Property(m => m.Type)
                .HasColumnName("type")
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(m => m.Payload)
                .HasColumnName("payload")
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(m => m.OccurredOnUtc).HasColumnName("occurred_on");
            builder.Property(m => m.ProcessedOnUtc).HasColumnName("processed_on");
            builder.Property(m => m.Error).HasColumnName("error");

            // R-09/docs/database/03 §10: ix_outbox_pending WHERE processed_on IS NULL
            builder.HasIndex(m => new { m.ProcessedOnUtc, m.OccurredOnUtc })
                .HasDatabaseName("ix_outbox_pending")
                .HasFilter("processed_on IS NULL");
        });
}
