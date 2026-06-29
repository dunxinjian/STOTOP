using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAuxiliaryItemConfiguration : IEntityTypeConfiguration<FinAuxiliaryItem>
{
    public void Configure(EntityTypeBuilder<FinAuxiliaryItem> builder)
    {
        builder.ToTable("FIN辅助核算项目");
            
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
            
        // 扩展字段：账套维度辅助核算
        builder.Property(e => e.FAccountSetId).HasColumnName("F账套ID").HasDefaultValue(0L);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID").HasDefaultValue(0L);
        builder.Property(e => e.FAuxType).HasColumnName("F辅助类型").HasMaxLength(50);
        builder.Property(e => e.FShortName).HasColumnName("F简称").HasMaxLength(100);
        builder.Property(e => e.FContact).HasColumnName("F联系人").HasMaxLength(100);
        builder.Property(e => e.FPhone).HasColumnName("F电话").HasMaxLength(50);
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(255);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FEnableStatus).HasColumnName("F启用状态").HasDefaultValue(1);

        // 来源字段
        builder.Property(e => e.FSourceType).HasColumnName("F来源类型").HasMaxLength(20);
        builder.Property(e => e.FSourceId).HasColumnName("F来源ID");
            
        builder.HasIndex(e => e.FCode).HasDatabaseName("IX_FIN辅助核算项目_编码");
        builder.HasIndex(new[] { "FAccountSetId", "FAuxType" }).HasDatabaseName("IX_FIN辅助核算项目_账套类型");
    }
}
