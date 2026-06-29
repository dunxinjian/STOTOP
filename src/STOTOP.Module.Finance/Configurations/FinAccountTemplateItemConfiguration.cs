using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountTemplateItemConfiguration : IEntityTypeConfiguration<FinAccountTemplateItem>
{
    public void Configure(EntityTypeBuilder<FinAccountTemplateItem> builder)
    {
        builder.ToTable("FIN科目模板_明细");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTemplateId).HasColumnName("F模板ID");
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FCategory).HasColumnName("F类别").HasMaxLength(20);
        builder.Property(e => e.FBalanceDirection).HasColumnName("F余额方向").HasMaxLength(10);
        builder.Property(e => e.FLevel).HasColumnName("F级次");
        builder.Property(e => e.FParentId).HasColumnName("F父ID").HasDefaultValue(0L);
        builder.Property(e => e.FIsLeaf).HasColumnName("F是否末级");
        builder.Property(e => e.FAuxiliary).HasColumnName("F辅助核算").HasMaxLength(200);
        builder.Property(e => e.FCurrency).HasColumnName("F外币").HasMaxLength(20);
        builder.Property(e => e.FUnit).HasColumnName("F计算单位").HasMaxLength(20);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序").HasDefaultValue(0);
        
        builder.HasOne<FinAccountTemplate>()
            .WithMany()
            .HasForeignKey(e => e.FTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.FTemplateId, e.FCode }).IsUnique().HasDatabaseName("IX_FIN科目模板明细_模板ID_编码");
        builder.HasIndex(e => e.FParentId).HasDatabaseName("IX_FIN科目模板明细_父ID");
    }
}
