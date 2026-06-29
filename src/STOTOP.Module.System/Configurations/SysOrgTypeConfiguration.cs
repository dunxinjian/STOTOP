using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysOrgTypeConfiguration : IEntityTypeConfiguration<SysOrgType>
{
    public void Configure(EntityTypeBuilder<SysOrgType> builder)
    {
        builder.ToTable("SYS组织类型");

        // 主键（手动指定，非自增）
        builder.HasKey(e => e.FID);
        builder.Property(e => e.FID)
            .HasColumnName("FID")
            .ValueGeneratedNever();

        builder.Property(e => e.FCode)
            .HasColumnName("F编码")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.FName)
            .HasColumnName("F名称")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.FLevel)
            .HasColumnName("F层级")
            .IsRequired();

        builder.Property(e => e.FCanBindAccountSet)
            .HasColumnName("F可关联账套")
            .HasDefaultValue(false);

        builder.Property(e => e.FCanSwitch)
            .HasColumnName("F可切换")
            .HasDefaultValue(false);

        builder.Property(e => e.FIcon)
            .HasColumnName("F图标")
            .HasMaxLength(50);

        builder.Property(e => e.FSortOrder)
            .HasColumnName("F排序")
            .HasDefaultValue(0);

        builder.Property(e => e.FDescription)
            .HasColumnName("F说明")
            .HasMaxLength(200);

        builder.Property(e => e.FIsEnabled)
            .HasColumnName("F是否启用")
            .HasDefaultValue(true);

        builder.Property(e => e.FCreateTime)
            .HasColumnName("F创建时间")
            .HasDefaultValueSql("GETDATE()");

        // 唯一索引
        builder.HasIndex(e => e.FCode)
            .IsUnique()
            .HasDatabaseName("UQ_SYS组织类型_F编码");
    }
}
