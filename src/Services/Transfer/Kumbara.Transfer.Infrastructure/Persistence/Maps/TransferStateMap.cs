using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Kumbara.Transfer.Domain.Entities;

namespace Kumbara.Transfer.Infrastructure.Persistence.Maps;

public class TransferStateMap : SagaClassMap<TransferState>
{
    protected override void Configure(EntityTypeBuilder<TransferState> entity, ModelBuilder model)
    {
        entity.ToTable("TransferStates");

        entity.Property(x => x.CurrentState)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.SourceAccountId)
            .IsRequired();

        entity.Property(x => x.DestinationAccountId)
            .IsRequired();

        entity.Property(x => x.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        entity.Property(x => x.Currency)
            .HasMaxLength(8)
            .IsRequired();

        entity.Property(x => x.FailureReason)
            .HasMaxLength(500);

        entity.Property(x => x.CreatedAt)
            .IsRequired();

        entity.Property(x => x.CompletedAt);

        entity.Property(x => x.RowVersion)
            .IsRowVersion();
    }
}
