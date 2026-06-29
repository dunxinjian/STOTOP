using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountConfiguration : IEntityTypeConfiguration<FinAccount>
{
    public void Configure(EntityTypeBuilder<FinAccount> builder)
    {
        builder.ToTable("FIN科目");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FCategory).HasColumnName("F类别").HasMaxLength(20);
        builder.Property(e => e.FBalanceDirection).HasColumnName("F余额方向").HasMaxLength(10);
        builder.Property(e => e.FLevel).HasColumnName("F级次");
        builder.Property(e => e.FParentId).HasColumnName("F父ID");
        builder.Property(e => e.FIsLeaf).HasColumnName("F是否末级");
        builder.Property(e => e.FAuxiliary).HasColumnName("F辅助核算").HasMaxLength(200);
        builder.Property(e => e.FCurrency).HasColumnName("F外币").HasMaxLength(20);
        builder.Property(e => e.FUnit).HasColumnName("F计算单位").HasMaxLength(20);
        builder.Property(e => e.FEnableStatus).HasColumnName("F启用状态");
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID").HasDefaultValue(0L);
        builder.Property(e => e.F启用年度).HasColumnName("F启用年度").HasDefaultValue(0);
        builder.Property(e => e.F启用期间).HasColumnName("F启用期间").HasDefaultValue(0);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        
        builder.HasIndex(e => e.FAccountSetId).HasDatabaseName("IX_FIN科目_账套ID");
        builder.HasIndex(e => new { e.FCode, e.FAccountSetId }).IsUnique().HasDatabaseName("IX_FIN科目_编码_账套ID");
        builder.HasIndex(e => e.FParentId).HasDatabaseName("IX_FIN科目_父级ID");
        builder.HasIndex(e => e.FCategory).HasDatabaseName("IX_FIN科目_类别");
    }
}
