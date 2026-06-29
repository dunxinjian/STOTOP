using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysPositionConfiguration : IEntityTypeConfiguration<SysPosition>
{
    public void Configure(EntityTypeBuilder<SysPosition> builder)
    {
        builder.ToTable("SYS岗位");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUID).HasColumnName("FUID").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FDingTalkPositionId).HasColumnName("F钉钉职位ID").HasMaxLength(100);
        builder.Property(e => e.FDingTalkBindStatus).HasColumnName("F钉钉绑定状态").HasDefaultValue(0);
        builder.Property(e => e.FSort).HasColumnName("F排序").HasDefaultValue(0);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.FUpdateTime).HasColumnName("F更新时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("UQ_SYS岗位_编码");
    }
}
