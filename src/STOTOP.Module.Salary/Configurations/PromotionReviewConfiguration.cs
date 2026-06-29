using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Salary.Entities;

namespace STOTOP.Module.Salary.Configurations;

public class PromotionReviewConfiguration : IEntityTypeConfiguration<PromotionReview>
{
    public void Configure(EntityTypeBuilder<PromotionReview> builder)
    {
        builder.ToTable("SAL晋升评审");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.F员工ID).HasColumnName("F员工ID");
        builder.Property(e => e.F规则ID).HasColumnName("F规则ID");
        builder.Property(e => e.F当前档位ID).HasColumnName("F当前档位ID");
        builder.Property(e => e.F目标档位ID).HasColumnName("F目标档位ID");
        builder.Property(e => e.F触发时间).HasColumnName("F触发时间");
        builder.Property(e => e.FA分快照).HasColumnName("FA分快照");
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F评审人ID).HasColumnName("F评审人ID");
        builder.Property(e => e.F评审时间).HasColumnName("F评审时间");
        builder.Property(e => e.F评审意见).HasColumnName("F评审意见").HasMaxLength(512);
        builder.Property(e => e.F生效日期).HasColumnName("F生效日期");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => new { e.FOrgId, e.F员工ID, e.F状态 }).HasDatabaseName("IX_SAL晋升评审_组织_员工_状态");
    }
}
