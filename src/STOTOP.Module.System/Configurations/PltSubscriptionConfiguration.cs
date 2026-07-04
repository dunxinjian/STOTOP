using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class PltSubscriptionConfiguration : IEntityTypeConfiguration<PltSubscription>
{
    public void Configure(EntityTypeBuilder<PltSubscription> builder)
    {
        builder.ToTable("PLT订阅");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTenantId).HasColumnName("F租户ID");
        builder.Property(e => e.FPlanId).HasColumnName("F套餐ID");
        builder.Property(e => e.FPeriodStart).HasColumnName("F周期起");
        builder.Property(e => e.FPeriodEnd).HasColumnName("F周期止");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FTenantId).HasDatabaseName("IX_PLT订阅_租户ID");
    }
}
