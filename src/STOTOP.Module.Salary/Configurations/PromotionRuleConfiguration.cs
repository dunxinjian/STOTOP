using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Configurations;

public class PromotionRuleConfiguration : IEntityTypeConfiguration<PromotionRule>
{
    public void Configure(EntityTypeBuilder<PromotionRule> builder)
    {
        builder.ToTable("SAL晋升规则");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F规则名称).HasColumnName("F规则名称").HasMaxLength(128).IsRequired();
        builder.Property(e => e.F当前档位ID).HasColumnName("F当前档位ID");
        builder.Property(e => e.F目标档位ID).HasColumnName("F目标档位ID");
        builder.Property(e => e.FA分阈值).HasColumnName("FA分阈值");
        builder.Property(e => e.F附加条件JSON).HasColumnName("F附加条件JSON").HasColumnType("nvarchar(max)");
        builder.Property(e => e.F启用状态).HasColumnName("F启用状态").HasDefaultValue(true);
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.F更新时间).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F当前档位ID, e.F目标档位ID }).IsUnique().HasDatabaseName("UQ_SAL晋升规则_组织_当前_目标");
    }
}
