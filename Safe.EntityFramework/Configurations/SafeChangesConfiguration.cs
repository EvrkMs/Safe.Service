using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Safe.Domain.Entities;

namespace Safe.EntityFramework.Configurations;

internal class SafeChangesConfiguration : IEntityTypeConfiguration<SafeChange>
{
    public void Configure(EntityTypeBuilder<SafeChange> b)
    {
        b.HasCheckConstraint("ck_safechange_amount_positive", "amount > 0");

        // Даты в UTC
        b.Property(x => x.CreatedAt)
         .HasDefaultValueSql("CURRENT_TIMESTAMP")
         .ValueGeneratedOnAdd();

        b.Property(x => x.OccurredAt)
         .HasDefaultValueSql("CURRENT_TIMESTAMP")
         .ValueGeneratedOnAdd();

        // Денежная точность
        b.Property(x => x.Amount).HasPrecision(18, 2);

        // Индексы для выборок
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Reason);
        b.HasIndex(x => x.CreatedBy);

        // Enum как строки
        b.Property(x => x.Status).HasConversion<string>().HasColumnType("text");
        b.Property(x => x.Direction).HasConversion<string>().HasColumnType("text");
        b.Property(x => x.Reason).HasConversion<string>().HasColumnType("text");

        // Audit fields
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(256);
        b.Property(x => x.ModifiedBy).HasMaxLength(256);

        // Optimistic concurrency - PostgreSQL xmin
        b.Property(x => x.RowVersion)
         .IsRowVersion()
         .HasColumnType("xid")
         .ValueGeneratedOnAddOrUpdate();
    }
}