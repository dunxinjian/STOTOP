using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Configurations;

public class PromotionHistoryConfiguration : IEntityTypeConfiguration<PromotionHistory>
{
    public void Configure(EntityTypeBuilder<PromotionHistory> builder)
    {
        builder.ToTable("SAL晋升历史");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F评审ID).HasColumnName("F评审ID");
        builder.Property(e => e.F原档位ID).HasColumnName("F原档位ID");
        builder.Property(e => e.F新档位ID).HasColumnName("F新档位ID");
        builder.Property(e => e.F原基本工资).HasColumnName("F原基本工资").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F新基本工资).HasColumnName("F新基本工资").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.F生效日期).HasColumnName("F生效日期");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID }).HasDatabaseName("IX_SAL晋升历史_组织_员工");
    }
}
