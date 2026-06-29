using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Configurations;

public class StgDynamicRecordConfiguration : IEntityTypeConfiguration<StgDynamicRecord>
{
    public void Configure(EntityTypeBuilder<StgDynamicRecord> builder)
    {
        builder.ToTable("STG通用导入数据");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F批次ID).HasColumnName("F批次ID");
        builder.Property(e => e.F原始行号).HasColumnName("F原始行号").HasDefaultValue(0);
        builder.Property(e => e.F业务主键).HasColumnName("F业务主键").HasMaxLength(256).IsRequired();
        builder.Property(e => e.F数据源类型).HasColumnName("F数据源类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.F动态数据).HasColumnName("F动态数据").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F处理状态).HasColumnName("F处理状态").HasDefaultValue(0);
        builder.Property(e => e.F错误信息).HasColumnName("F错误信息").HasMaxLength(int.MaxValue);
        builder.Property(e => e.F关联凭证ID).HasColumnName("F关联凭证ID");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.F批次ID).HasDatabaseName("IX_STG通用导入数据_F批次ID");
        builder.HasIndex(e => e.F数据源类型).HasDatabaseName("IX_STG通用导入数据_F数据源类型");

        // IStagingRecord / IOrgScoped 数据隔离字段
        builder.Property(e => e.FOrgId).HasColumnName("FOrgId");
        builder.Property(e => e.FDataScopeId).HasColumnName("FDataScopeId").HasMaxLength(64);
        builder.Property(e => e.FSourceWorkItemId).HasColumnName("FSourceWorkItemId");
        builder.Property(e => e.FIsRevoked).HasColumnName("FIsRevoked");
        builder.Property(e => e.F账套ID).HasColumnName("F账套ID");
        builder.Property(e => e.F归属网点编号).HasColumnName("F归属网点编号").HasMaxLength(50);
        builder.HasIndex(e => e.FDataScopeId).HasDatabaseName("IX_STG通用导入数据_数据作用域").HasFilter("[FDataScopeId] IS NOT NULL");
    }
}
