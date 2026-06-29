using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountMappingDetailConfiguration : IEntityTypeConfiguration<FinAccountMappingDetail>
{
    public void Configure(EntityTypeBuilder<FinAccountMappingDetail> builder)
    {
        builder.ToTable("F账套迁移_科目映射");
        builder.HasKey(e => e.FID);

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.F方案ID).HasColumnName("F方案ID");
        builder.Property(e => e.F源科目编码).HasColumnName("F源科目编码").HasMaxLength(50);
        builder.Property(e => e.F源科目名称).HasColumnName("F源科目名称").HasMaxLength(200);
        builder.Property(e => e.F目标科目ID).HasColumnName("F目标科目ID");
        builder.Property(e => e.F目标科目编码).HasColumnName("F目标科目编码").HasMaxLength(50);
        builder.Property(e => e.F目标科目名称).HasColumnName("F目标科目名称").HasMaxLength(200);
        builder.Property(e => e.F映射类型).HasColumnName("F映射类型");
        builder.Property(e => e.F条件JSON).HasColumnName("F条件JSON").HasMaxLength(2000);
        builder.Property(e => e.F优先级).HasColumnName("F优先级");
        builder.Property(e => e.F说明).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.F状态).HasColumnName("F状态");
        builder.Property(e => e.F创建时间).HasColumnName("F创建时间");

        builder.HasIndex(e => e.F方案ID).HasDatabaseName("IX_F账套迁移_科目映射_方案ID");
        builder.HasIndex(e => e.F源科目编码).HasDatabaseName("IX_F账套迁移_科目映射_源科目编码");
    }
}
