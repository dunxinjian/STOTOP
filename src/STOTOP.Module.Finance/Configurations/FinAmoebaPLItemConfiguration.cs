using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAmoebaPLItemConfiguration : IEntityTypeConfiguration<FinAmoebaPLItem>
{
    public void Configure(EntityTypeBuilder<FinAmoebaPLItem> builder)
    {
        builder.ToTable("FIN阿米巴损益项");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTemplateId).HasColumnName("F模板ID");
        builder.Property(e => e.FItemName).HasColumnName("F项目名称").HasMaxLength(100);
        builder.Property(e => e.FNodeRole).HasColumnName("F节点角色").HasMaxLength(20).HasDefaultValue("data").IsRequired();
        builder.Property(e => e.FFormula).HasColumnName("F计算公式").HasMaxLength(500);
        builder.Property(e => e.FSort).HasColumnName("F排序");
        builder.Property(e => e.FParentId).HasColumnName("F父ID");
        builder.Property(e => e.FRelatedAccountsJson).HasColumnName("F关联科目JSON");
        builder.Property(e => e.FDataSource).HasColumnName("F数据源").HasMaxLength(20);
        builder.Property(e => e.FSummaryKeywordsJson).HasColumnName("F摘要关键词JSON").HasMaxLength(500);
        builder.Property(e => e.FBillingFilterJson).HasColumnName("F计费过滤Json");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
        builder.Property(e => e.FUnit).HasColumnName("F单位").HasMaxLength(20);
        builder.Property(e => e.FDataSourceRemark).HasColumnName("F数据来源说明").HasMaxLength(200);
        builder.Property(e => e.FCalculationLogic).HasColumnName("F计算逻辑").HasMaxLength(500);
        builder.Property(e => e.FPerUnitMode).HasColumnName("F单票均模式").HasMaxLength(20);
        builder.Property(e => e.FIsManualEntry).HasColumnName("F是否手工填报");
        builder.Property(e => e.F项目类别).HasColumnName("F项目类别").HasMaxLength(20);
        builder.Property(e => e.F是否指标分区).HasColumnName("F是否指标分区").HasDefaultValue(false);
        builder.Property(e => e.F值来源).HasColumnName("F值来源").HasMaxLength(20);
        builder.Property(e => e.F系统数据源).HasColumnName("F系统数据源").HasMaxLength(20);
        builder.Property(e => e.F小数位数).HasColumnName("F小数位数");
        builder.Property(e => e.F分摊方式).HasColumnName("F分摊方式").HasMaxLength(20);
        builder.Property(e => e.F分摊基数).HasColumnName("F分摊基数").HasMaxLength(20);

        builder.HasIndex(e => e.FTemplateId).HasDatabaseName("IX_FIN阿米巴损益项_模板ID");
        builder.HasIndex(e => e.FParentId).HasDatabaseName("IX_FIN阿米巴损益项_父级ID");
        
        builder.HasOne<FinAmoebaPLTemplate>()
            .WithMany(t => t.Items)
            .HasForeignKey(e => e.FTemplateId)
            .HasConstraintName("FK_FIN阿米巴损益项_模板ID");
    }
}
